using System.Text;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;

namespace MarketplaceApi.Services
{
    public interface ISearchService
    {
        Task<List<ProductResponseDto>> SearchProductsAsync(string query, int maxResults = 20);
    }

    /// <summary>
    /// A lightweight, dependency-free fuzzy search over products that's tolerant
    /// of typos and minor spelling mistakes (works for Arabic and English).
    /// Strategy:
    ///   1. Normalize text (lowercase, strip diacritics/tashkeel, unify common
    ///      Arabic letter variants, collapse whitespace).
    ///   2. Pull a reasonably small candidate set from the DB.
    ///   3. Score each candidate using a mix of: exact substring match,
    ///      per-token Levenshtein distance, and a starts-with bonus.
    ///   4. Return results sorted by best score, above a minimum threshold.
    /// </summary>
    public class SearchService : ISearchService
    {
        private readonly AppDbContext _db;

        public SearchService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<ProductResponseDto>> SearchProductsAsync(string query, int maxResults = 20)
        {
            var normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) return new List<ProductResponseDto>();

            var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Pull all products into memory for scoring. For very large catalogs,
            // replace this with a pre-filter (e.g. trigram index / Elasticsearch).
            var products = await _db.Products.Include(p => p.Category).ToListAsync();

            var scored = products
                .Select(p => new
                {
                    Product = p,
                    Score = ScoreProduct(p.Name, p.Description, normalizedQuery, queryTokens)
                })
                .Where(x => x.Score > 0.35) // similarity threshold
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .ToList();

            return scored.Select(x => new ProductResponseDto
            {
                Id = x.Product.Id,
                Name = x.Product.Name,
                ImageUrl = x.Product.ImageUrl,
                Price = x.Product.Price,
                Description = x.Product.Description,
                CoinsPerUnit = x.Product.CoinsPerUnit,
                CategoryId = x.Product.CategoryId,
                CategoryName = x.Product.Category?.Name,
                CreatedAtUtc = x.Product.CreatedAtUtc
            }).ToList();
        }

        private static double ScoreProduct(string name, string? description, string normalizedQuery, string[] queryTokens)
        {
            var normalizedName = Normalize(name);
            var normalizedDesc = Normalize(description ?? string.Empty);

            double best = 0;

            // 1) Exact substring match on the full query -> very strong signal.
            if (normalizedName.Contains(normalizedQuery)) best = Math.Max(best, 1.0);
            else if (normalizedDesc.Contains(normalizedQuery)) best = Math.Max(best, 0.6);

            // 2) Token-level fuzzy matching against the product name's tokens.
            var nameTokens = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var qToken in queryTokens)
            {
                double bestTokenScore = 0;
                foreach (var nToken in nameTokens)
                {
                    if (nToken == qToken) { bestTokenScore = 1.0; break; }
                    if (nToken.StartsWith(qToken) || qToken.StartsWith(nToken))
                        bestTokenScore = Math.Max(bestTokenScore, 0.85);

                    var distance = LevenshteinDistance(qToken, nToken);
                    var maxLen = Math.Max(qToken.Length, nToken.Length);
                    if (maxLen == 0) continue;

                    var similarity = 1.0 - (double)distance / maxLen;
                    // Allow ~1 typo per 4 characters.
                    if (similarity >= 0.6) bestTokenScore = Math.Max(bestTokenScore, similarity);
                }
                best = Math.Max(best, bestTokenScore * 0.9);
            }

            return best;
        }

        private static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in input.Trim().ToLowerInvariant())
            {
                // Strip Arabic diacritics (tashkeel) U+064B..U+0652.
                if (ch >= '\u064B' && ch <= '\u0652') continue;

                var mapped = ch switch
                {
                    'أ' or 'إ' or 'آ' => 'ا',
                    'ى' => 'ي',
                    'ة' => 'ه',
                    'ؤ' => 'و',
                    'ئ' => 'ي',
                    _ => ch
                };

                if (char.IsWhiteSpace(mapped))
                {
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                }
                else
                {
                    sb.Append(mapped);
                }
            }

            return sb.ToString().Trim();
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var dp = new int[a.Length + 1, b.Length + 1];

            for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[a.Length, b.Length];
        }
    }
}
