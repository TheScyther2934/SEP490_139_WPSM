import { useSelector } from "react-redux";
import { selectCustomerState } from "../store/customerSlice";

// The customer's own project loaded by CustomerLayout; `project` is null when the account has none yet.
export function useCustomerProject() {
  const { status, error, snapshot } = useSelector(selectCustomerState);
  return { loading: status === "idle" || status === "loading", error, project: snapshot?.project ?? null };
}
