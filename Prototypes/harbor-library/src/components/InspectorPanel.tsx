import { ExternalLink, FileText, Lightbulb, Plus, Star, Trash2, X } from "lucide-react";
import type { ReferenceItem } from "../types";

interface InspectorPanelProps {
  item: ReferenceItem;
  open: boolean;
  onClose: () => void;
  onChange: (item: ReferenceItem) => void;
  onFavorite: () => void;
  onDelete: () => void;
}

export function InspectorPanel({ item, open, onClose, onChange, onFavorite, onDelete }: InspectorPanelProps) {
  const setField = (field: "title" | "description" | "source", value: string) => {
    onChange({ ...item, [field]: value });
  };

  return (
    <aside className={`inspector${open ? " open" : ""}`}>
      <header className="inspector-header">
        <span>{"\u0421\u0432\u043e\u0439\u0441\u0442\u0432\u0430"}</span>
        <button className="icon-button quiet" onClick={onClose} type="button" aria-label="Close inspector">
          <X size={19} />
        </button>
      </header>

      <div className="inspector-scroll">
        <input
          className="title-input"
          value={item.title}
          onChange={(event) => setField("title", event.target.value)}
          aria-label="Title"
        />

        <section className="property-section">
          <label htmlFor="description">{"\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435"}</label>
          <textarea
            id="description"
            value={item.description}
            onChange={(event) => setField("description", event.target.value)}
            rows={5}
          />
        </section>

        <section className="property-section">
          <label htmlFor="source">{"\u0418\u0441\u0442\u043e\u0447\u043d\u0438\u043a"}</label>
          <div className="source-field">
            <div>
              <input id="source" value={item.source} onChange={(event) => setField("source", event.target.value)} />
              <small>{item.domain}</small>
            </div>
            <ExternalLink size={16} />
          </div>
        </section>

        <section className="property-section">
          <label>{"\u0422\u0435\u0433\u0438"}</label>
          <div className="tag-row inspector-tags">
            {item.tags.map((tag) => <span className="tag" key={tag}>{tag}<button type="button">&times;</button></span>)}
            <button className="add-tag" type="button"><Plus size={15} /></button>
          </div>
        </section>

        <section className="property-section related-section">
          <div className="section-heading">
            <label>{"\u0421\u0432\u044f\u0437\u0430\u043d\u043d\u044b\u0435 \u0438\u0434\u0435\u0438"}</label>
            <span>{item.ideas.length}</span>
          </div>
          <div className="related-list">
            {item.ideas.map((idea, index) => (
              <button type="button" key={idea}>
                {index < 2 ? <Lightbulb size={16} /> : <FileText size={16} />}
                <span>{idea}</span>
              </button>
            ))}
          </div>
          <button className="link-idea" type="button"><Plus size={15} />{" \u0421\u0432\u044f\u0437\u0430\u0442\u044c \u0438\u0434\u0435\u044e"}</button>
        </section>
      </div>

      <footer className="inspector-footer">
        <span>{"\u0418\u0437\u043c\u0435\u043d\u0435\u043d\u043e \u0441\u0435\u0433\u043e\u0434\u043d\u044f"}</span>
        <div>
          <button className={`icon-button${item.favorite ? " active" : ""}`} onClick={onFavorite} type="button" aria-label="Favorite">
            <Star size={18} fill={item.favorite ? "currentColor" : "none"} />
          </button>
          <button className="icon-button danger" onClick={onDelete} type="button" aria-label="Delete"><Trash2 size={18} /></button>
        </div>
      </footer>
    </aside>
  );
}
