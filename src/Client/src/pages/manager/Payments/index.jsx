import { useState } from "react";
import { useDispatch } from "react-redux";
import { Button, Typography } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import PaymentTable from "../../../components/PaymentTable";
import BalanceSummary from "./components/BalanceSummary";
import { useProject } from "../../../hooks/useProject";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// M-13, read-only for now: recording T-30/final payments (M-14) waits for its API.
// The NRD receipt is fetched with the Bearer token as a blob, then saved from a local object URL.
export default function Payments() {
  const { project } = useProject();
  const dispatch = useDispatch();
  const [downloading, setDownloading] = useState(false);

  const openReceipt = async () => {
    setDownloading(true);
    try {
      const blob = await projectService.downloadNrdReceipt(project.id);
      // window.open after an await is blocked as a pop-up; a download link is not.
      const url = URL.createObjectURL(blob);
      const link = Object.assign(document.createElement("a"), { href: url, download: `NRD-${project.id}` });
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setDownloading(false);
    }
  };

  return (
    <>
      <Typography variant="h6" sx={{ mb: 2 }}>Thanh toán và dư nợ (M-13)</Typography>
      <BalanceSummary project={project} />
      <PaymentTable payments={project.payments ?? []} action={(p) => p.type === "NRD" && p.hasReceipt && (
        <Button size="small" startIcon={<DownloadIcon />} onClick={openReceipt} disabled={downloading}>Biên nhận NRD</Button>
      )} />
    </>
  );
}
