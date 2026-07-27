import {
  Archive,
  ChevronDown,
  CircleCheck,
  ClipboardList,
  Compass,
  Inbox,
  LayoutDashboard,
  Lightbulb,
  Map,
  PanelsTopLeft,
  Settings2,
  Sparkles,
  Star,
  Trash2,
} from "lucide-react";

const projectItems = [
  { label: "\u041e\u0431\u0437\u043e\u0440", icon: LayoutDashboard },
  { label: "\u0420\u0435\u0444\u0435\u0440\u0435\u043d\u0441\u044b", icon: PanelsTopLeft, active: true },
  { label: "\u0418\u0434\u0435\u0438", icon: Lightbulb },
  { label: "\u041f\u043b\u0430\u043d\u044b", icon: ClipboardList },
  { label: "\u0414\u043e\u0441\u043a\u0438", icon: Map },
  { label: "\u0417\u0430\u0434\u0430\u0447\u0438", icon: CircleCheck, count: 8 },
];

const utilityItems = [
  { label: "\u0412\u0445\u043e\u0434\u044f\u0449\u0438\u0435", icon: Inbox, count: 3 },
  { label: "\u0418\u0437\u0431\u0440\u0430\u043d\u043d\u043e\u0435", icon: Star },
  { label: "\u0410\u0440\u0445\u0438\u0432", icon: Archive },
  { label: "\u041a\u043e\u0440\u0437\u0438\u043d\u0430", icon: Trash2 },
];

export function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="window-dots" aria-hidden="true">
        <span />
        <span />
        <span />
      </div>

      <button className="project-switcher" type="button">
        <span className="project-mark"><Compass size={17} /></span>
        <span className="project-name">Harbor Market</span>
        <ChevronDown size={15} />
      </button>

      <nav className="nav-group" aria-label="Project">
        {projectItems.map((item) => (
          <button className={`nav-item${item.active ? " active" : ""}`} key={item.label} type="button">
            <item.icon size={18} strokeWidth={1.75} />
            <span>{item.label}</span>
            {item.count && <span className="nav-count">{item.count}</span>}
          </button>
        ))}
      </nav>

      <div className="sidebar-rule" />

      <nav className="nav-group utility" aria-label="Library">
        {utilityItems.map((item) => (
          <button className="nav-item" key={item.label} type="button">
            <item.icon size={18} strokeWidth={1.75} />
            <span>{item.label}</span>
            {item.count && <span className="nav-count warm">{item.count}</span>}
          </button>
        ))}
      </nav>

      <div className="sidebar-note">
        <Sparkles size={15} />
        <span>{"2 \u0447\u0435\u043b\u043e\u0432\u0435\u043a\u0430 \u0432 \u043f\u0440\u043e\u0435\u043a\u0442\u0435"}</span>
      </div>

      <div className="sync-row">
        <span className="sync-dot" />
        <div>
          <strong>{"\u0421\u0438\u043d\u0445\u0440\u043e\u043d\u0438\u0437\u0438\u0440\u043e\u0432\u0430\u043d\u043e"}</strong>
          <small>{"\u0422\u043e\u043b\u044c\u043a\u043e \u0447\u0442\u043e"}</small>
        </div>
        <button className="icon-button quiet" type="button" aria-label="Settings"><Settings2 size={17} /></button>
      </div>
    </aside>
  );
}
