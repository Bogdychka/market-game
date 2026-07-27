import { Star, UserRound } from "lucide-react";
import type { ReferenceItem } from "../types";

interface ReferenceCardProps {
  item: ReferenceItem;
  selected: boolean;
  atlasUrl: string;
  onSelect: (id: number) => void;
}

export function ReferenceCard({ item, selected, atlasUrl, onSelect }: ReferenceCardProps) {
  return (
    <article
      className={`reference-card ${item.accent}${selected ? " selected" : ""}`}
      onClick={() => onSelect(item.id)}
      tabIndex={0}
      onKeyDown={(event) => event.key === "Enter" && onSelect(item.id)}
    >
      <div
        className="card-image"
        style={{ backgroundImage: `url(${atlasUrl})`, backgroundPosition: item.atlasPosition }}
        role="img"
        aria-label={item.title}
      >
        <span className="card-index">0{item.id}</span>
        {item.favorite && <Star className="favorite-star" size={18} fill="currentColor" />}
      </div>
      <div className="card-body">
        <h2>{item.title}</h2>
        <p>{item.note}</p>
        <div className="tag-row">
          {item.tags.map((tag) => <span className="tag" key={tag}>{tag}</span>)}
        </div>
      </div>
      {item.editor && (
        <div className="editor-presence">
          <span className="avatar"><UserRound size={13} /></span>
          <span><strong>{item.editor}</strong>{" \u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u0443\u0435\u0442"}</span>
          <span className="presence-pulse" />
        </div>
      )}
    </article>
  );
}
