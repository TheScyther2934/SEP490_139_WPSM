import { isoDate } from "../../../utils/format";

// Date maths for M-21. Anchors are "YYYY-MM-DD" strings so the view state can live on the URL.

export const parseDate = (value) => new Date(`${String(value).slice(0, 10)}T00:00:00`);

export function eachDay(from, to) {
  const days = [];
  const start = parseDate(from);
  const end = parseDate(to);
  for (let day = new Date(start); day <= end; day.setDate(day.getDate() + 1)) days.push(isoDate(day));
  return days;
}

/** Day → the day itself; Week → Monday..Sunday; Month → the whole calendar month. */
export function rangeFor(view, anchor) {
  const date = parseDate(anchor);
  if (view === "day") return { from: anchor, to: anchor, days: [anchor] };
  if (view === "month") {
    const first = new Date(date.getFullYear(), date.getMonth(), 1);
    const last = new Date(date.getFullYear(), date.getMonth() + 1, 0);
    const from = isoDate(first);
    const to = isoDate(last);
    return { from, to, days: eachDay(from, to) };
  }
  const monday = new Date(date);
  monday.setDate(date.getDate() - ((date.getDay() + 6) % 7));
  const from = isoDate(monday);
  const to = isoDate(new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 6));
  return { from, to, days: eachDay(from, to) };
}

export function shiftAnchor(view, anchor, delta) {
  const date = parseDate(anchor);
  if (view === "month") date.setMonth(date.getMonth() + delta, 1);
  else if (view === "week") date.setDate(date.getDate() + delta * 7);
  else date.setDate(date.getDate() + delta);
  return isoDate(date);
}

// Local (venue, UTC+7) date + time → the UTC instant the API stores.
export const toIsoUtc = (date, time) => new Date(`${String(date).slice(0, 10)}T${time || "00:00"}:00`).toISOString();

export const timeOfIso = (iso) => {
  const date = new Date(iso);
  return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
};

export const dateOfIso = (iso) => isoDate(new Date(iso));

export const hhmm = (value) => String(value ?? "").slice(0, 5);
