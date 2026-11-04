import { useCallback, useEffect, useRef, useState } from "react";
import { errorMessage } from "../utils/format";

// Loads data for one screen. Answers from an older call are dropped, so fast filter changes never show stale rows.
// `silent` reloads (polling) keep the current data on screen instead of flashing a spinner.
export function useAsync(load, deps = []) {
  const [state, setState] = useState({ data: null, error: null, loading: true });
  const version = useRef(0);
  const loadRef = useRef(load);
  loadRef.current = load;

  const run = useCallback(async ({ silent = false } = {}) => {
    const current = ++version.current;
    if (!silent) setState((s) => ({ ...s, loading: true, error: null }));
    try {
      const data = await loadRef.current();
      if (current === version.current) setState({ data, error: null, loading: false });
    } catch (error) {
      if (current === version.current) setState((s) => ({ data: silent ? s.data : null, error: errorMessage(error), loading: false }));
    }
  }, []);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { run(); }, deps);

  const setData = useCallback((update) => setState((s) => ({ ...s, data: typeof update === "function" ? update(s.data) : update })), []);
  return { ...state, reload: run, setData };
}

// Re-runs `tick` every `ms` while the tab is visible (A-01, A-13 refresh every 30 s).
export function usePolling(tick, ms = 30000) {
  const tickRef = useRef(tick);
  tickRef.current = tick;
  useEffect(() => {
    const id = window.setInterval(() => { if (document.visibilityState === "visible") tickRef.current(); }, ms);
    return () => window.clearInterval(id);
  }, [ms]);
}
