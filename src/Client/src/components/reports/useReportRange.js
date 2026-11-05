import { useSearchParams } from "react-router-dom";
import { isoDate } from "../../utils/format";

// A-09..A-12 keep the date range on the query string so reload and shared links keep the filter.
// Default: the current month.
export function useReportRange() {
  const [params, setParams] = useSearchParams();
  const now = new Date();
  const from = params.get("from") || isoDate(new Date(now.getFullYear(), now.getMonth(), 1));
  const to = params.get("to") || isoDate(new Date(now.getFullYear(), now.getMonth() + 1, 0));
  const setRange = (next) => setParams({ from: next.from, to: next.to }, { replace: true });
  const valid = !!from && !!to && from <= to;
  return { from, to, valid, query: `?from=${from}&to=${to}`, setRange };
}
