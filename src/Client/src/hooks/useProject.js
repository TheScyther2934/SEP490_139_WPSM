import { useSelector } from "react-redux";
import { selectProjectState } from "../store/projectSlice";

// Pages under ProjectLayout render only after the snapshot is loaded, so these values are present.
export function useProject() {
  const { snapshot } = useSelector(selectProjectState);
  return { project: snapshot.project, roadmap: snapshot.roadmap ?? [], catalog: snapshot.catalog ?? [], priceFrames: snapshot.priceFrames ?? [] };
}
