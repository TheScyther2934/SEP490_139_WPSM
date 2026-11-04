import { useDispatch } from "react-redux";
import { Alert } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PaymentTable from "../../../components/PaymentTable";
import BalanceCards from "./components/BalanceCards";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { loadCustomerProject } from "../../../store/customerSlice";

// C-10: payment milestones and balance of the customer's own project, read-only.
export default function Payments() {
  const dispatch = useDispatch();
  const { loading, error, project } = useCustomerProject();
  return (
    <>
      <PageHeader code="C-10" title="Thanh toán và dư nợ" />
      <DataState loading={loading} error={error} onRetry={() => dispatch(loadCustomerProject())}>
        {project ? (
          <>
            <BalanceCards project={project} />
            <PaymentTable payments={project.payments ?? []} />
          </>
        ) : <Alert severity="info">Tài khoản hiện chưa có hồ sơ hợp đồng.</Alert>}
      </DataState>
    </>
  );
}
