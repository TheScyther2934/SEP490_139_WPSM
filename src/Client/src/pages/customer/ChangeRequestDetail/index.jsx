import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { Box, Button, Stack } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import TextDialog from "../../../components/TextDialog";
import CostBreakdown from "../../../components/changeRequests/CostBreakdown";
import { CrExceptions, CrHistory, CrItemsTable, CrSummary } from "../../../components/changeRequests/CrDetailParts";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { changeRequestService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-07 detail and cost; C-08 approve / reject dialog; "request adjustment" adds a comment (UC-09, UC-10).
export default function ChangeRequestDetail() {
  const { crCode } = useParams();
  const { project } = useCustomerProject();
  const { data: cr, loading, error, reload, setData } = useAsync(
    () => (project ? changeRequestService.get(project.id, crCode) : Promise.resolve(null)), [project?.id, crCode]);
  const { busy, run } = useAction();
  const [dialog, setDialog] = useState(null);

  const act = async (call, message) => {
    const updated = await run(call, message);
    if (updated) { setData(updated); setDialog(null); }
  };
  const negotiating = cr?.status === "Negotiating";

  return (
    <>
      <PageHeader code="C-07" title={`Yêu cầu thay đổi ${crCode}`}
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.changeRequests}>Danh sách CR</Button>} />
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
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }} id="crActions">
                {cr.status === "Draft" && (
                  <Button variant="contained" disabled={busy} onClick={() => act(() => changeRequestService.submit(project.id, crCode), "Đã gửi yêu cầu thay đổi (MSG19).")}>
                    Gửi thương lượng
                  </Button>
                )}
                {negotiating && !cr.customerApprovedAt && <Button variant="contained" color="success" onClick={() => setDialog("approve")}>Duyệt</Button>}
                {negotiating && <Button variant="outlined" onClick={() => setDialog("comment")}>Đề nghị điều chỉnh</Button>}
                {(negotiating || cr.status === "Draft") && <Button color="error" onClick={() => setDialog("reject")}>Từ chối</Button>}
                {cr.addendumCode && <Button component={RouterLink} to={PATHS.customer.addenda}>Xem phụ lục {cr.addendumCode}</Button>}
              </Stack>
            </Stack>
          </Box>
        )}
      </DataState>

      <TextDialog open={dialog === "approve"} title="Duyệt yêu cầu thay đổi" busy={busy} confirmText="Duyệt" color="success"
        description={`Bạn đồng ý với chi phí của ${crCode}? Khi Manager cũng duyệt, phụ lục hợp đồng sẽ được tạo.`}
        onClose={() => setDialog(null)} onConfirm={() => act(() => changeRequestService.approve(project.id, crCode), "Đã ghi nhận quyết định (MSG20).")} />
      <TextDialog open={dialog === "reject"} title="Từ chối yêu cầu thay đổi" label="Lý do" maxLength={300} busy={busy} confirmText="Từ chối" color="error"
        description="CR bị từ chối sẽ đóng lại và không mở lại được."
        onClose={() => setDialog(null)} onConfirm={(reason) => act(() => changeRequestService.reject(project.id, crCode, reason), "Đã ghi nhận quyết định (MSG20).")} />
      <TextDialog open={dialog === "comment"} title="Đề nghị điều chỉnh" label="Nội dung góp ý" required busy={busy} confirmText="Gửi"
        onClose={() => setDialog(null)} onConfirm={(note) => act(() => changeRequestService.comment(project.id, crCode, note), "Đã gửi góp ý cho Manager.")} />
    </>
  );
}
