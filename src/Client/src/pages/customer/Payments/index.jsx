import { Alert, Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PaymentTable from "../../../components/PaymentTable";
import PaymentSummary from "../../../components/PaymentSummary";
import FileButton from "../../../components/FileButton";
import { useAsync } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { paymentService } from "../../../services/operationsService";

// C-10 (UC-12): milestones and balance of the customer's own project; the settlement receipt appears once Settled.
export default function Payments() {
  const { loading: projectLoading, error: projectError, project } = useCustomerProject();
  const { data, loading, error, reload } = useAsync(
    () => (project ? paymentService.get(project.id) : Promise.resolve(null)), [project?.id]);
  return (
    <>
      <PageHeader code="C-10" title="Thanh toán và dư nợ"
        actions={data?.settlementReceiptFileId && <FileButton fileId={data.settlementReceiptFileId} variant="contained">Biên nhận quyết toán</FileButton>} />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload}>
        {data ? (
          <Stack>
            <PaymentSummary payments={data} />
            <PaymentTable payments={data.payments} action={(p) => p.receiptFileId && <FileButton fileId={p.receiptFileId}>Chứng từ</FileButton>} />
          </Stack>
        ) : <Alert severity="info">Tài khoản hiện chưa có hồ sơ hợp đồng.</Alert>}
      </DataState>
    </>
  );
}
