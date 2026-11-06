import { useEffect, useState } from "react";
import { useDispatch } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Divider, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import SaveIcon from "@mui/icons-material/Save";
import SendIcon from "@mui/icons-material/Send";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../PageHeader";
import DataState from "../DataState";
import CrItemEditor from "./CrItemEditor";
import CostPreviewPanel from "./CostPreviewPanel";
import { CR_TYPES, emptyRow, previewCost, rowIssue, toCommand, amount } from "./crMeta";
import { useAsync } from "../../hooks/useAsync";
import { changeRequestService } from "../../services/changeRequestService";
import { notify } from "../../store/notifySlice";
import { dateVi, errorMessage, money, slotLabel } from "../../utils/format";

const REASON_MAX = 500;

// C-06 Create CR Ticket (UC-07, UC-08) — also used by the Manager to log a ticket for the Customer.
// `afterSaveTo(code)` decides where the flow continues: the Customer returns to C-05, the Manager opens M-08.
// The editor is keyed by the draft code so switching between drafts remounts it with the right values.
export default function ChangeRequestForm(props) {
  const [params] = useSearchParams();
  const crCode = params.get("crCode");
  return <CrFormEditor key={crCode ?? "new"} crCode={crCode} {...props} />;
}

function CrFormEditor({ role = "Customer", projectCode, crCode, screenCode = "C-06", cancelTo, afterSaveTo }) {
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const [type, setType] = useState("");
  const [reason, setReason] = useState("");
  const [rows, setRows] = useState([]);
  const [seeded, setSeeded] = useState(false);
  const [attempted, setAttempted] = useState(false);
  const [saving, setSaving] = useState(null);
  const [serverError, setServerError] = useState("");

  const { data, loading, error, reload } = useAsync(async () => {
    const form = await changeRequestService.getForm(role, projectCode);
    const ticket = crCode ? await changeRequestService.getDetail(role, projectCode, crCode) : null;
    return { form, ticket };
  });

  const form = data?.form ?? null;
  const ticket = data?.ticket ?? null;

  // Seed the editor once: an existing draft keeps its values, a new ticket starts with one empty line.
  useEffect(() => {
    if (!data || seeded) return;
    if (ticket) {
      setType(ticket.type);
      setReason(ticket.description);
      setRows(ticket.items.map((i) => ({
        specItemId: String(i.specItemId),
        newQuantity: String(i.newQuantity),
        newUnitPrice: String(i.newUnitPrice),
      })));
    } else {
      setRows([emptyRow()]);
    }
    setSeeded(true);
  }, [data, ticket, seeded]);

  const update = (index, patch) => setRows((list) => list.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  const remove = (index) => setRows((list) => list.filter((_, i) => i !== index));
  const add = () => setRows((list) => [...list, emptyRow()]);

  const reasonError = !reason.trim()
    ? "Trường Lý do thay đổi là bắt buộc."
    : reason.trim().length > REASON_MAX ? `Vượt quá độ dài tối đa ${REASON_MAX} ký tự.` : "";
  const typeError = type ? "" : "Trường Loại thay đổi là bắt buộc.";
  const issues = form ? rows.map((row) => rowIssue(form, rows, row)) : [];
  const rowsValid = rows.length > 0 && issues.every((issue) => !issue);
  const blocked = form?.blockedReason ?? "";
  const valid = !!form && !blocked && !typeError && !reasonError && rowsValid;
  const cost = previewCost(form, rows);
  const editing = !!crCode;

  const save = async (submit) => {
    setAttempted(true);
    setServerError("");
    if (!valid || saving) return;
    setSaving(submit ? "submit" : "draft");
    try {
      const body = { type, description: reason.trim(), items: toCommand(rows), submit };
      const result = editing
        ? await changeRequestService.updateDraft(role, projectCode, crCode, body)
        : await changeRequestService.create(role, projectCode, body);
      dispatch(notify(submit ? "Đã gửi yêu cầu thay đổi." : "Đã lưu bản nháp yêu cầu thay đổi."));
      navigate(afterSaveTo(result.code), { replace: true });
    } catch (e) {
      setServerError(errorMessage(e));
      setSaving(null);
    }
  };

  const title = editing ? "Chỉnh sửa yêu cầu thay đổi" : "Tạo yêu cầu thay đổi (CR)";
  const subtitle = form
    ? `${form.coupleName} · ${form.hall} · Ca ${slotLabel(form.slot)} · ${dateVi(form.eventDate)} · còn ${form.daysToEvent} ngày`
    : "Đề xuất điều chỉnh dịch vụ, số lượng hoặc giá trị sau khi hợp đồng gốc đã chốt.";

  return (
    <>
      <PageHeader code={screenCode} title={title} subtitle={subtitle}
        actions={<Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={() => navigate(cancelTo)}>Quay lại danh sách</Button>} />

      <DataState loading={loading} error={error} onRetry={reload}>
        {form && (
          <Stack spacing={2}>
            {blocked && <Alert severity="info">{blocked}</Alert>}
            {editing && ticket && ticket.status !== "Draft" && (
              <Alert severity="warning">Chỉ bản nháp mới có thể chỉnh sửa. Yêu cầu {ticket.code} đang ở trạng thái {ticket.status}.</Alert>
            )}
            {serverError && <Alert severity="error" role="alert">{serverError}</Alert>}

            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
              <Stack spacing={2}>
                <Card>
                  <CardContent>
                    <Typography variant="h6">Thông tin chung</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                      Mô tả loại thay đổi và lý do nghiệp vụ để Quản lý thẩm định giá.
                    </Typography>
                    <Stack spacing={2}>
                      <TextField select id="crType" label="Loại thay đổi" value={type} required disabled={!!blocked || saving}
                        onChange={(e) => setType(e.target.value)} error={attempted && !!typeError}
                        helperText={attempted && typeError ? typeError : "Chọn nhóm thay đổi gần nhất với yêu cầu."}>
                        {CR_TYPES.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
                      </TextField>
                      <TextField id="crReason" label="Lý do thay đổi" required multiline minRows={3} value={reason}
                        disabled={!!blocked || saving} onChange={(e) => setReason(e.target.value)}
                        error={attempted && !!reasonError} helperText={attempted && reasonError ? reasonError : `${reason.trim().length}/${REASON_MAX} ký tự`}
                        slotProps={{ htmlInput: { maxLength: REASON_MAX } }} />
                    </Stack>
                  </CardContent>
                </Card>

                <Card>
                  <CardContent>
                    <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                      <Box>
                        <Typography variant="h6">Hạng mục thay đổi cụ thể</Typography>
                        <Typography variant="body2" color="text.secondary">Chọn hạng mục từ baseline đã chốt và nhập giá trị mới.</Typography>
                      </Box>
                      <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={add}
                        disabled={!!blocked || saving || rows.length >= 20 || rows.length >= (form.specItems.length || 1)}>
                        Thêm hạng mục
                      </Button>
                    </Stack>
                    <Stack spacing={2} sx={{ mt: 2 }}>
                      {rows.map((row, index) => (
                        <CrItemEditor key={index} form={form} row={row} index={index} rows={rows}
                          showErrors={attempted} onChange={(patch) => update(index, patch)} onRemove={() => remove(index)} />
                      ))}
                      {rows.length === 0 && (
                        <Typography color="text.secondary" sx={{ py: 2, textAlign: "center" }}>
                          Chưa có hạng mục nào. Chọn “Thêm hạng mục” để bắt đầu.
                        </Typography>
                      )}
                    </Stack>
                    <Divider sx={{ my: 2 }} />
                    <Typography variant="caption" color="text.secondary">
                      Đơn giá mới phải nằm trong khung giá hiện hành của từng dịch vụ (GBR-02). Baseline gốc không bị thay đổi;
                      mọi điều chỉnh chỉ có hiệu lực qua phụ lục hợp đồng sau khi hai bên phê duyệt.
                    </Typography>
                  </CardContent>
                </Card>
              </Stack>

              <CostPreviewPanel form={form} cost={cost} />
            </Box>

            <Card>
              <CardContent>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
                  <Typography variant="body2" color="text.secondary">
                    Tổng giá trị hợp đồng hiện tại {money(amount(form.currentTotal))} · {rows.length} hạng mục ·{" "}
                    {rowsValid ? "Sẵn sàng gửi" : "Cần hoàn tất các trường bắt buộc"}
                  </Typography>
                  <Stack direction="row" spacing={1}>
                    <Button variant="outlined" startIcon={<SaveIcon />} onClick={() => save(false)}
                      disabled={!!blocked || !!saving} id="saveCrDraftBtn">
                      {saving === "draft" ? "Đang lưu…" : "Lưu bản nháp"}
                    </Button>
                    <Button variant="contained" startIcon={<SendIcon />} onClick={() => save(true)}
                      disabled={!!blocked || !!saving} id="submitCrBtn">
                      {saving === "submit" ? "Đang gửi…" : editing ? "Lưu & gửi yêu cầu" : "Gửi yêu cầu thay đổi"}
                    </Button>
                  </Stack>
                </Stack>
              </CardContent>
            </Card>
          </Stack>
        )}
      </DataState>
    </>
  );
}
