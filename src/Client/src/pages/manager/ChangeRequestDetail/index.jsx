import { Link as RouterLink, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import { Alert, Box, Button, Card, CardContent, Chip, Divider, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import CrComparisonTable from "../../../components/changeRequest/CrComparisonTable";
import CrCostBreakdown from "../../../components/changeRequest/CrCostBreakdown";
import CrTimeline from "../../../components/changeRequest/CrTimeline";
import { typeLabel } from "../../../components/changeRequest/crMeta";
import CrProgressStepper from "./components/CrProgressStepper";
import CounterCard from "./components/CounterCard";
import ExceptionCard from "./components/ExceptionCard";
import DecisionPanel from "./components/DecisionPanel";
import { useAsync } from "../../../hooks/useAsync";
import { changeRequestService } from "../../../services/changeRequestService";
import { loadProject } from "../../../store/projectSlice";
import { notify } from "../../../store/notifySlice";
import { dateTimeVi, dateVi, slotLabel } from "../../../utils/format";
import { PATHS } from "../../../routes/paths";

// M-08 CR Detail and Negotiation (UC-22, UC-23). The Manager reviews the cost impact, sends a
// counter-proposal, asks the Admin for a penalty waiver, then approves or rejects with a reason.
export default function ChangeRequestDetail() {
  const { projectCode, crCode } = useParams();
  const dispatch = useDispatch();
  const { data: ticket, loading, error, reload, setData } = useAsync(() => changeRequestService.getDetail("Manager", projectCode, crCode));

  const apply = (updated, message) => {
    setData(updated);
    dispatch(notify(message));
  };

  const counter = async (body) => {
    const updated = await changeRequestService.counter(projectCode, crCode, body);
    apply(updated, "Đã gửi đề xuất đối ứng tới khách hàng.");
  };

  const approve = async () => {
    const updated = await changeRequestService.approve("Manager", projectCode, crCode, null);
    apply(updated, updated.status === "Approved"
      ? "Đã phê duyệt và tạo phụ lục hợp đồng."
      : "Đã ghi nhận phê duyệt của Quản lý. CR chuyển Approved khi khách hàng phê duyệt.");
    // Approval may change the project total and the BEO status, so refresh the project header.
    dispatch(loadProject(projectCode));
  };

  const reject = async (reason) => {
    const updated = await changeRequestService.reject("Manager", projectCode, crCode, reason);
    apply(updated, "Đã từ chối yêu cầu thay đổi.");
  };

  const requestException = async (note) => {
    const updated = await changeRequestService.requestException(projectCode, crCode, note);
    apply(updated, "Đã gửi yêu cầu ngoại lệ tới Admin.");
  };

  const negotiating = ticket?.status === "Negotiating";

  return (
    <>
      <PageHeader code="M-08" title={ticket ? `${ticket.code} · ${typeLabel(ticket.type)}` : "Chi tiết yêu cầu thay đổi"}
        subtitle={ticket ? `${ticket.projectCode} · ${ticket.coupleName} · ${ticket.hall} · Ca ${slotLabel(ticket.slot)} · ${dateVi(ticket.eventDate)}` : "Thương lượng và phê duyệt yêu cầu thay đổi."}
        actions={ticket && (
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.changeRequests(projectCode)}>Danh sách CR</Button>
            {ticket.status === "Approved" && (
              <Button variant="outlined" startIcon={<ReceiptLongIcon />} component={RouterLink} to={PATHS.manager.costImpact(projectCode, ticket.code)}>
                Tác động chi phí &amp; phụ lục
              </Button>
            )}
          </Stack>
        )} />

      <DataState loading={loading} error={error} onRetry={reload}>
        {ticket && (
          <Stack spacing={2}>
            <Card>
              <CardContent>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                  <Typography variant="h6" color="primary">{ticket.code}</Typography>
                  <Chip size="small" variant="outlined" label={typeLabel(ticket.type)} />
                  <StatusChip kind="cr" status={ticket.status} />
                  {ticket.addendumCode && <Chip size="small" color="success" label={`Phụ lục ${ticket.addendumCode}`} />}
                  {ticket.pendingException === "Pending" && <Chip size="small" color="warning" label="Chờ Admin ngoại lệ" />}
                </Stack>
                <Typography sx={{ mt: 1 }}>{ticket.description}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Người tạo: {ticket.createdByName} ({ticket.createdByRole === "Customer" ? "Khách hàng" : "Quản lý tạo"}) · {dateTimeVi(ticket.createdAt)}
                </Typography>
                <CrProgressStepper ticket={ticket} />
              </CardContent>
            </Card>

            {ticket.pendingException === "Pending" && (
              <Alert severity="info">Đang chờ Admin quyết định miễn phí phạt cho yêu cầu này. CR vẫn tiếp tục thương lượng.</Alert>
            )}
            {ticket.status === "Approved" && ticket.beoStatus === "Stale" && (
              <Alert severity="warning">
                BEO đã lỗi thời do yêu cầu thay đổi được duyệt. Vui lòng duyệt lại BEO để gửi hướng dẫn mới cho các bộ phận (MSG22).
              </Alert>
            )}
            {ticket.status === "Rejected" && (
              <Alert severity="error">Yêu cầu đã bị từ chối. Lý do được lưu trong lịch sử thương lượng bên dưới.</Alert>
            )}

            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
              <Stack spacing={2}>
                <CrComparisonTable items={ticket.items} />
                <CrCostBreakdown cost={ticket.cost} />
                <CrTimeline history={ticket.history} />
              </Stack>

              <Stack spacing={2}>
                {negotiating ? (
                  <>
                    <CounterCard ticket={ticket} onCounter={counter} />
                    <ExceptionCard ticket={ticket} onRequest={requestException} />
                    <DecisionPanel ticket={ticket} onApprove={approve} onReject={reject} />
                  </>
                ) : (
                  <Card>
                    <CardContent>
                      <Typography variant="h6" gutterBottom>Yêu cầu đã đóng</Typography>
                      <Stack spacing={1}>
                        <Stack direction="row" sx={{ justifyContent: "space-between" }}>
                          <Typography variant="body2" color="text.secondary">Quản lý phê duyệt</Typography>
                          <Typography variant="body2">{ticket.managerApprovedAt ? dateTimeVi(ticket.managerApprovedAt) : "Chưa duyệt"}</Typography>
                        </Stack>
                        <Stack direction="row" sx={{ justifyContent: "space-between" }}>
                          <Typography variant="body2" color="text.secondary">Khách hàng phê duyệt</Typography>
                          <Typography variant="body2">{ticket.customerApprovedAt ? dateTimeVi(ticket.customerApprovedAt) : "Chưa duyệt"}</Typography>
                        </Stack>
                        <Stack direction="row" sx={{ justifyContent: "space-between" }}>
                          <Typography variant="body2" color="text.secondary">Phụ lục hợp đồng</Typography>
                          <Typography variant="body2">{ticket.addendumCode ?? "Chưa tạo"}</Typography>
                        </Stack>
                      </Stack>
                      <Divider sx={{ my: 2 }} />
                      <Typography variant="body2" color="text.secondary">
                        Không thể thương lượng tiếp vì yêu cầu không còn ở trạng thái Negotiating.
                      </Typography>
                    </CardContent>
                  </Card>
                )}
              </Stack>
            </Box>
          </Stack>
        )}
      </DataState>
    </>
  );
}
