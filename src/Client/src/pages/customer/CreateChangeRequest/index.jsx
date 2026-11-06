import ChangeRequestForm from "../../../components/changeRequest/ChangeRequestForm";
import { PATHS } from "../../../routes/paths";

// C-06 Create CR Ticket (UC-07, UC-08). The API resolves the Customer's own project (GBR-12).
// Both "Save draft" and "Submit" stay on this screen; after saving the flow returns to C-05.
export default function CreateChangeRequest() {
  return (
    <ChangeRequestForm role="Customer" screenCode="C-06"
      cancelTo={PATHS.customer.changeRequests}
      afterSaveTo={() => PATHS.customer.changeRequests} />
  );
}
