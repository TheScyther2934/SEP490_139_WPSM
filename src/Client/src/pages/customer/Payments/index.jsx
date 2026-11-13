import { useState } from "react";
import { useDispatch } from "react-redux";
import { Alert, Box, Typography } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import FinancialSummaryCards from "./components/FinancialSummaryCards";
import PaymentMilestonesTable from "./components/PaymentMilestonesTable";
import FinalSettlementReceiptSection from "./components/FinalSettlementReceiptSection";
import { useAsync } from "../../../hooks/useAsync";
import { paymentService, saveBlobAsFile } from "../../../services/paymentService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// C-10: Payment Milestones & Balance (Customer)
// Tracks 5 financial metrics, milestone payments, and valid receipts. Read-only for Customer.
export default function Payments() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => paymentService.getOverview());
  const [downloadingType, setDownloadingType] = useState(null);
  const [downloadingFinal, setDownloadingFinal] = useState(false);

  const handleDownloadReceipt = async (paymentType) => {
    try {
      setDownloadingType(paymentType);
      const res = await paymentService.downloadReceipt(paymentType, data?.summary?.projectCode);
      saveBlobAsFile(res.blob, res.fileName);
      dispatch(notify({ message: `Đã tải xuống biên nhận đợt ${paymentType}.`, severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setDownloadingType(null);
    }
  };

  const handleDownloadFinalReceipt = async () => {
    try {
      setDownloadingFinal(true);
      const res = await paymentService.downloadFinalReceipt(data?.summary?.projectCode);
      saveBlobAsFile(res.blob, res.fileName);
      dispatch(notify({ message: "Đã tải xuống biên nhận quyết toán.", severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setDownloadingFinal(false);
    }
  };

  const projectCode = data?.summary?.projectCode;
  const projectStatus = data?.summary?.projectStatus;

  return (
    <Box>
      <PageHeader
        code="C-10"
        title="Thanh toán và dư nợ"
        subtitle={projectCode ? `Dự án: ${projectCode} (${projectStatus})` : undefined}
      />

      <DataState loading={loading} error={error} onRetry={reload}>
        {data === null ? (
          <Alert severity="info">Tài khoản hiện chưa có hồ sơ hợp đồng.</Alert>
        ) : (
          data && (
            <>
              <FinancialSummaryCards summary={data.summary} />

              <Typography variant="h6" component="h2" sx={{ mb: 1.5, fontWeight: 600 }}>
                Tiến độ thanh toán theo mốc
              </Typography>
              <PaymentMilestonesTable
                milestones={data.milestones}
                onDownloadReceipt={handleDownloadReceipt}
                downloadingType={downloadingType}
              />

              <FinalSettlementReceiptSection
                finalReceipt={data.finalReceipt}
                onDownload={handleDownloadFinalReceipt}
                downloading={downloadingFinal}
              />
            </>
          )
        )}
      </DataState>
    </Box>
  );
}
