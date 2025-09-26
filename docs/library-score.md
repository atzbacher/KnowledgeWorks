# Library score column

The Library results grid only shows a numeric **Score** when the list comes from a full-text search.  Each result wraps the underlying entry together with the optional relevance score that arrives from the search index; metadata-only queries (filters, tags, etc.) populate entries without a score so the column remains blank for them.

The score itself is a normalized [BM25](https://en.wikipedia.org/wiki/Okapi_BM25) relevance value reported by the SQLite FTS5 engine.  The raw BM25 numbers are clamped to zero or above and converted with `1 / (1 + raw)` so that the UI always receives a floating-point value between 0 (no relevance) and 1 (best possible match).  Because BM25 yields lower numbers for better matches, this normalization ensures that higher values in the column reflect stronger matches to the search terms.

| Score value | Meaning |
|-------------|---------|
| `~1.000`    | Near-perfect relevance for the entered search terms. |
| `0.500`     | Moderately relevant match. |
| `~0.000`    | Very weak relevance; the hit barely satisfied the query. |
| Blank       | The entry came from a metadata/filter search instead of full-text search. |

Tip: sort the grid by the **Score** column in descending order to see the most relevant full-text hits first.
