import { useParams } from "react-router-dom";
import ChangeRequestForm from "../../../components/changeRequest/ChangeRequestForm";
import { PATHS } from "../../../routes/paths";

// M-07 "Create CR on Behalf of Customer": the same C-06 form, opened inside the Manager project.
// The creator is stored as the Manager and the flow continues on M-08 (UC-22 alternative flow).
export default function CreateChangeRequest() {
  const { projectCode } = useParams();
  return (
    <ChangeRequestForm role="Manager" projectCode={projectCode} screenCode="M-07"
      cancelTo={PATHS.manager.changeRequests(projectCode)}
      afterSaveTo={(crCode) => PATHS.manager.changeRequest(projectCode, crCode)} />
  );
}
