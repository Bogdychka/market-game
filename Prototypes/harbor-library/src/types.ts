export type AtlasPosition = `${number}% ${number}%`;

export interface ReferenceItem {
  id: number;
  title: string;
  note: string;
  description: string;
  source: string;
  domain: string;
  tags: string[];
  ideas: string[];
  atlasPosition: AtlasPosition;
  accent: "violet" | "blue" | "green" | "amber";
  favorite?: boolean;
  editor?: string;
}
