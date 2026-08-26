using System.Globalization;
using System.Text;

namespace KaragoPublisher.Windows;

public sealed class CategoryCatalog
{
    public string Site { get; }
    public IReadOnlyList<WordPressCategory> All { get; }
    public HashSet<int> SelectedIds { get; } = [];
    private readonly Dictionary<int,WordPressCategory> byId;

    public CategoryCatalog(string site, IEnumerable<WordPressCategory> categories)
    {
        if (site != "diafaneia") throw new InvalidOperationException("Άγνωστο site κατηγοριών.");
        Site = site;
        var input = categories.ToList();
        if (input.Any(c => c.Site != site)) throw new InvalidOperationException("Απορρίφθηκαν κατηγορίες άλλου site.");
        var list = input.Where(c => c.Id > 0).DistinctBy(c => c.Id).ToList();
        All = list; byId = list.ToDictionary(c => c.Id);
    }

    public IReadOnlyList<WordPressCategory> Frequent(int count = 6) => All.OrderByDescending(c => c.Count).ThenBy(c => c.Name).Take(count).ToList();
    public IReadOnlyList<WordPressCategory> Match(IEnumerable<string> suggestions) => suggestions.SelectMany(s => All.Where(c => N(c.Name) == N(s) || N(c.Slug) == N(s) || c.Id.ToString() == s.Trim())).DistinctBy(c => c.Id).ToList();
    public void Preselect(IEnumerable<string> suggestions) { foreach (var c in Match(suggestions)) SelectedIds.Add(c.Id); }
    public void Select(int id, bool selected) { if (!byId.ContainsKey(id)) return; if (selected) SelectedIds.Add(id); else SelectedIds.Remove(id); }
    public IReadOnlyList<WordPressCategory> Selected() => SelectedIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    public IReadOnlyList<WordPressCategory> Compact(IEnumerable<string> suggestions) => Frequent().Concat(Match(suggestions)).Concat(Selected()).DistinctBy(c => c.Id).ToList();
    public IReadOnlyList<WordPressCategory> Search(string search) => string.IsNullOrWhiteSpace(search) ? Hierarchy(All) : Hierarchy(All.Where(c => N(c.Name).Contains(N(search)) || N(c.Slug).Contains(N(search))).ToList());
    public string DisplayName(WordPressCategory category) { var depth = 0; var parent = category.Parent; var seen = new HashSet<int>(); while (parent > 0 && byId.TryGetValue(parent, out var p) && seen.Add(parent)) { depth++; parent = p.Parent; } return new string('—', depth) + (depth > 0 ? " " : "") + category.Name; }

    private IReadOnlyList<WordPressCategory> Hierarchy(IEnumerable<WordPressCategory> source)
    {
        var visible = source.Select(c => c.Id).ToHashSet();
        foreach (var id in visible.ToList()) { var parent = byId[id].Parent; while (parent > 0 && byId.ContainsKey(parent) && visible.Add(parent)) parent = byId[parent].Parent; }
        var output = new List<WordPressCategory>(); var visited = new HashSet<int>();
        void Add(int parent) { foreach (var c in All.Where(c => c.Parent == parent && visible.Contains(c.Id)).OrderBy(c => c.Name)) if (visited.Add(c.Id)) { output.Add(c); Add(c.Id); } }
        Add(0); output.AddRange(All.Where(c => visible.Contains(c.Id) && !visited.Contains(c.Id))); return output;
    }
    private static string N(string value) => new string(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Trim();
}
