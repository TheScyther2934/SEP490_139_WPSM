import { useState } from "react";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { Box, Button, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DataState from "../../../components/DataState";
import TextDialog from "../../../components/TextDialog";
import CostBreakdown from "../../../components/changeRequests/CostBreakdown";
import { CrExceptions, CrHistory, CrItemsTable, CrSummary } from "../../../components/changeRequests/CrDetailParts";
import CounterProposalDialog from "./components/CounterProposalDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { changeRequestService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// M-08 (UC-22, UC-23): review, counter-propose, approve, reject, request a penalty waiver (UC-47).
export default function ChangeRequestDetail() {
  const { crCode } = useParams();
  const { project } = useProject();
  const navigate = useNavigate();
  const { data: cr, loading, error, reload, setData } = useAsync(() => changeRequestService.get(project.id, crCode), [project.id, crCode]);
  const { busy, run } = useAction();
  const [dialog, setDialog] = useState(null);

  const act = async (call, message) => {
    const updated = await run(call, message);
    if (!updated) return;
    setData(updated);
    setDialog(null);
    if (updated.status === "Approved") navigate(PATHS.manager.costImpact(project.id, crCode));
  };
  const negotiating = cr?.status === "Negotiating";
  const pendingException = cr?.exceptions?.some((e) => e.status === "Pending");

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">CR {crCode} (M-08)</Typography>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.changeRequests(project.id)}>Danh sách CR</Button>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload} empty={!cr} emptyText="Không tìm thấy yêu cầu thay đổi.">
        {cr && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "3fr 2fr" }, alignItems: "start" }}>
            <Stack spacing={2}>
              <CrSummary cr={cr} />
              <CrExceptions exceptions={cr.exceptions} />
              <CrItemsTable items={cr.items} />
              <CrHistory history={cr.history} />
            </Stack>
            <Stack spacing={2}>
              <CostBreakdown cost={cr.cost} approved={cr.status === "Approved"} estimate={negotiating} />
              <Stack direction="row" sx={{ flexWrap: "wrap", gap: 1 }} id="crActions">
                {cr.status === "Draft" && cr.createdByRole === "Manager" && (
                  <Button variant="contained" disabled={busy} onClick={() => act(() => changeRequestService.submit(project.id, crCode), "Đã gửi yêu cầu thay đổi (MSG19).")}>
                    Gửi thương lượng
                  </Button>
                )}
                {negotiating && !cr.managerApprovedAt && (
                  <Button variant="contained" color="success" disabled={pendingException} onClick={() => setDialog("approve")}>Duyệt</Button>
                )}
                {negotiating && <Button variant="outlined" onClick={() => setDialog("counter")}>Đề xuất lại</Button>}
                {negotiating && <Button variant="outlined" onClick={() => setDialog("comment")}>Góp ý</Button>}
                {negotiating && !pendingException && <Button variant="outlined" color="warning" onClick={() => setDialog("exception")}>Yêu cầu ngoại lệ</Button>}
                {(negotiating || cr.status === "Draft") && <Button color="error" onClick={() => setDialog("reject")}>Từ chối</Button>}
                {cr.status === "Approved" && (
                  <Button variant="contained" component={RouterLink} to={PATHS.manager.costImpact(project.id, crCode)}>Chi phí và phụ lục</Button>
                )}
              </Stack>
            </Stack>
          </Box>
        )}
      </DataState>

      {cr && (
        <CounterProposalDialog open={dialog === "counter"} projectCode={project.id} cr={cr} busy={busy} onClose={() => setDialog(null)}
          onSave={(items, note) => act(() => changeRequestService.counterPropose(project.id, crCode, items, note), "Đã lưu đề xuất và báo khách hàng.")} />
      )}
      <TextDialog open={dialog === "approve"} title="Duyệt yêu cầu thay đổi" busy={busy} confirmText="Duyệt" color="success"
        description="Khi cả hai bên đã duyệt, CR chuyển sang Đã duyệt, phụ lục hợp đồng được tạo và tổng hợp đồng được cập nhật (GBR-04)."
        onClose={() => setDialog(null)} onConfirm={() => act(() => changeRequestService.approve(project.id, crCode), "Đã ghi nhận quyết định (MSG20).")} />
      <TextDialog open={dialog === "reject"} title="Từ chối yêu cầu thay đổi" label="Lý do từ chối" required maxLength={300} busy={busy}
        confirmText="Từ chối" color="error" description="CR bị từ chối sẽ đóng lại; lịch sử vẫn được giữ (GBR-13)."
        onClose={() => setDialog(null)} onConfirm={(reason) => act(() => changeRequestService.reject(project.id, crCode, reason), "Đã ghi nhận quyết định (MSG20).")} />
      <TextDialog open={dialog === "comment"} title="Góp ý cho khách hàng" label="Nội dung góp ý" required busy={busy} confirmText="Gửi"
        onClose={() => setDialog(null)} onConfirm={(note) => act(() => changeRequestService.comment(project.id, crCode, note), "Đã gửi góp ý.")} />
      <TextDialog open={dialog === "exception"} title="Yêu cầu ngoại lệ (miễn phí phạt)" label="Ghi chú" required maxLength={300} busy={busy}
        confirmText="Gửi Admin" color="warning" description="Admin xem xét ở màn A-18. CR chưa thể duyệt cho tới khi Admin quyết định."
        onClose={() => setDialog(null)} onConfirm={(note) => act(() => changeRequestService.requestException(project.id, crCode, note), "Đã gửi yêu cầu ngoại lệ tới Admin.")} />
    </>
  );
}
