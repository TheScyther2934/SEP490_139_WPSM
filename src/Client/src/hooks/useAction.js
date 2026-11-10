import { useCallback, useState } from "react";
import { useDispatch } from "react-redux";
import { notify } from "../store/notifySlice";
import { errorMessage } from "../utils/format";

// Runs one mutation: shows the success toast or the API message, and reports busy so buttons can be disabled.
// Resolves to the result (or true), or undefined when the call failed.
export function useAction() {
  const dispatch = useDispatch();
  const [busy, setBusy] = useState(false);
  const run = useCallback(async (action, success) => {
    setBusy(true);
    try {
      const result = await action();
      if (success) dispatch(notify({ message: success }));
      return result ?? true;
    } catch (error) {
      dispatch(notify({ message: errorMessage(error), severity: "error" }));
      return undefined;
    } finally {
      setBusy(false);
    }
  }, [dispatch]);
  return { busy, run };
}
