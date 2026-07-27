import { useMemo, useState } from "react";
import { ArrowDownNarrowWide, Filter, LayoutGrid, Plus, Search, SlidersHorizontal, X } from "lucide-react";
import { initialReferences } from "./data";
import { InspectorPanel } from "./components/InspectorPanel";
import { ReferenceCard } from "./components/ReferenceCard";
import { Sidebar } from "./components/Sidebar";
import type { ReferenceItem } from "./types";

const atlasUrl = "/reference-atlas.png";

function App() {
  const [references, setReferences] = useState(initialReferences);
  const [selectedId, setSelectedId] = useState(1);
  const [query, setQuery] = useState("");
  const [activeTag, setActiveTag] = useState<string | null>(null);
  const [inspectorOpen, setInspectorOpen] = useState(true);
  const [filterOpen, setFilterOpen] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const selected = references.find((item) => item.id === selectedId) ?? references[0];
  const tags = useMemo(() => [...new Set(references.flatMap((item) => item.tags))].slice(0, 6), [references]);
  const filtered = useMemo(() => {
    const normalized = query.toLocaleLowerCase();
    return references.filter((item) => {
      const matchesSearch = !normalized || `${item.title} ${item.note} ${item.tags.join(" ")}`.toLocaleLowerCase().includes(normalized);
      return matchesSearch && (!activeTag || item.tags.includes(activeTag));
    });
  }, [references, query, activeTag]);

  const selectReference = (id: number) => {
    setSelectedId(id);
    setInspectorOpen(true);
  };

  const updateSelected = (updated: ReferenceItem) => {
    setReferences((items) => items.map((item) => item.id === updated.id ? updated : item));
  };

  const deleteSelected = () => {
    if (!selected) return;
    const remaining = references.filter((item) => item.id !== selected.id);
    setReferences(remaining);
    setSelectedId(remaining[0]?.id ?? 0);
    setInspectorOpen(Boolean(remaining.length));
    showToast("\u041c\u0430\u0442\u0435\u0440\u0438\u0430\u043b \u043f\u0435\u0440\u0435\u043c\u0435\u0449\u0435\u043d \u0432 \u043a\u043e\u0440\u0437\u0438\u043d\u0443");
  };

  const addReference = () => {
    const nextId = Math.max(...references.map((item) => item.id), 0) + 1;
    const draft: ReferenceItem = {
      ...initialReferences[0],
      id: nextId,
      title: "\u041d\u043e\u0432\u044b\u0439 \u0440\u0435\u0444\u0435\u0440\u0435\u043d\u0441",
      note: "\u0414\u043e\u0431\u0430\u0432\u044c\u0442\u0435 \u043a\u043e\u0440\u043e\u0442\u043a\u0443\u044e \u0437\u0430\u043c\u0435\u0442\u043a\u0443 \u043e \u0442\u043e\u043c, \u0447\u0442\u043e \u043f\u043e\u043d\u0440\u0430\u0432\u0438\u043b\u043e\u0441\u044c.",
      description: "",
      source: "\u0411\u0435\u0437 \u0438\u0441\u0442\u043e\u0447\u043d\u0438\u043a\u0430",
      domain: "draft",
      tags: ["\u0447\u0435\u0440\u043d\u043e\u0432\u0438\u043a"],
      ideas: [],
      atlasPosition: "50% 0%",
      accent: "green",
    };
    setReferences((items) => [draft, ...items]);
    setSelectedId(nextId);
    setInspectorOpen(true);
    showToast("\u0427\u0435\u0440\u043d\u043e\u0432\u0438\u043a \u0434\u043e\u0431\u0430\u0432\u043b\u0435\u043d");
  };

  const showToast = (message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 2200);
  };

  return (
    <div className="app-shell">
      <Sidebar />
      <main className="workspace">
        <header className="workspace-header">
          <div>
            <div className="eyebrow">{"\u0411\u0438\u0431\u043b\u0438\u043e\u0442\u0435\u043a\u0430 / Harbor Market"}</div>
            <h1>{"\u0420\u0435\u0444\u0435\u0440\u0435\u043d\u0441\u044b"} <span>{references.length}</span></h1>
          </div>
          <div className="header-actions">
            <div className="search-box">
              <Search size={17} />
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={"\u041f\u043e\u0438\u0441\u043a \u043f\u043e \u0431\u0438\u0431\u043b\u0438\u043e\u0442\u0435\u043a\u0435..."} />
              {query && <button type="button" onClick={() => setQuery("")}><X size={15} /></button>}
            </div>
            <div className="filter-wrap">
              <button className={`toolbar-button${filterOpen ? " active" : ""}`} type="button" onClick={() => setFilterOpen((value) => !value)}>
                <Filter size={17} /><span>{"\u0424\u0438\u043b\u044c\u0442\u0440"}</span>
              </button>
              {filterOpen && (
                <div className="filter-popover">
                  <small>{"\u0422\u0435\u0433\u0438"}</small>
                  <button className={!activeTag ? "active" : ""} onClick={() => setActiveTag(null)} type="button">{"\u0412\u0441\u0435"}</button>
                  {tags.map((tag) => <button className={activeTag === tag ? "active" : ""} onClick={() => setActiveTag(tag)} type="button" key={tag}>{tag}</button>)}
                </div>
              )}
            </div>
            <button className="toolbar-button sort" type="button"><ArrowDownNarrowWide size={17} /><span>{"\u041d\u0435\u0434\u0430\u0432\u043d\u0438\u0435"}</span></button>
            <button className="icon-button view active" type="button" aria-label="Grid view"><LayoutGrid size={18} /></button>
            <button className="add-button" type="button" onClick={addReference}><Plus size={21} /><span>{"\u0414\u043e\u0431\u0430\u0432\u0438\u0442\u044c"}</span></button>
          </div>
        </header>

        {activeTag && (
          <div className="active-filter">
            <SlidersHorizontal size={15} />
            <span>{"\u0422\u0435\u0433"}: {activeTag}</span>
            <button type="button" onClick={() => setActiveTag(null)}><X size={14} /></button>
          </div>
        )}

        <section className="gallery-scroll">
          {filtered.length ? (
            <div className="reference-grid">
              {filtered.map((item) => (
                <ReferenceCard key={item.id} item={item} selected={item.id === selectedId} atlasUrl={atlasUrl} onSelect={selectReference} />
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <Search size={28} />
              <h2>{"\u041d\u0438\u0447\u0435\u0433\u043e \u043d\u0435 \u043d\u0430\u0448\u043b\u0438"}</h2>
              <p>{"\u041f\u043e\u043f\u0440\u043e\u0431\u0443\u0439\u0442\u0435 \u0434\u0440\u0443\u0433\u043e\u0439 \u0437\u0430\u043f\u0440\u043e\u0441 \u0438\u043b\u0438 \u0441\u0431\u0440\u043e\u0441\u044c\u0442\u0435 \u0444\u0438\u043b\u044c\u0442\u0440."}</p>
            </div>
          )}
        </section>
      </main>

      {selected && (
        <InspectorPanel
          item={selected}
          open={inspectorOpen}
          onClose={() => setInspectorOpen(false)}
          onChange={updateSelected}
          onFavorite={() => updateSelected({ ...selected, favorite: !selected.favorite })}
          onDelete={deleteSelected}
        />
      )}
      {!inspectorOpen && selected && <button className="reopen-inspector" type="button" onClick={() => setInspectorOpen(true)}>{"\u0421\u0432\u043e\u0439\u0441\u0442\u0432\u0430"}</button>}
      {toast && <div className="toast"><span className="sync-dot" />{toast}</div>}
    </div>
  );
}

export default App;
