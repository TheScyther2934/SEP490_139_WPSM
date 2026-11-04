import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Alert, Box, Button, Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import CustomerSection from "./components/CustomerSection";
import ContractSection from "./components/ContractSection";
import NrdSection from "./components/NrdSection";
import SummaryCard from "./components/SummaryCard";
import ConfirmDialog from "./components/ConfirmDialog";
import SuccessPanel from "./components/SuccessPanel";
import { requiredNrd, validateProject } from "./validation";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { configurationService } from "../../../services/configurationService";
import { PATHS } from "../../../routes/paths";
import { errorMessage, moneyInput } from "../../../utils/format";

const EMPTY = { coupleName: "", customerEmail: "", customerPhone: "", hall: "", eventDate: "", slot: "", baseValue: "", nrdAmount: "", paymentMethod: "Bank", receipt: null };

// M-03. Creating the project and uploading the receipt are two calls; when the upload fails the project
// already exists, so the retry only re-sends the receipt (never a second POST /projects).
export default function CreateProject() {
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [created, setCreated] = useState(null); // { project, temporaryPassword, emailStatus, receiptPending }

  const policy = useAsync(() => configurationService.getPolicies());
  const halls = useAsync(() => projectService.getHalls(form.eventDate || undefined), [form.eventDate]);
  const hallList = halls.data ?? [];
  const slots = hallList.find((h) => h.name === form.hall)?.slots ?? [];
  const minimum = requiredNrd(form, policy.data);
  const locked = !!created;

  const change = (key, value) => setForm((f) => ({
    ...f, [key]: value,
    // A new hall or date invalidates the chosen slot.
    ...(key === "hall" || key === "eventDate" ? { slot: "" } : {}),
  }));

  const review = () => {
    const next = validateProject(form, policy.data, slots);
    setErrors(next);
    setSubmitError("");
    if (Object.keys(next).length) { setSubmitError("Vui lòng kiểm tra các trường được đánh dấu."); return; }
    setConfirmOpen(true);
  };

  const uploadReceipt = async (project) => {
    if (!form.receipt) return true;
    try {
      await projectService.uploadNrdReceipt(project.id, form.receipt);
      return true;
    } catch (e) {
      setSubmitError(`Dự án ${project.id} đã được tạo nhưng chưa lưu được biên nhận: ${errorMessage(e)} Vui lòng chọn lại tệp và tải lên.`);
      return false;
    }
  };

  const create = async () => {
    setSaving(true);
    setSubmitError("");
    try {
      const result = await projectService.createProject({
        coupleName: form.coupleName.trim(), customerEmail: form.customerEmail.trim(), customerPhone: form.customerPhone.trim(),
        hall: form.hall, slot: form.slot, eventDate: form.eventDate,
        baseValue: moneyInput(form.baseValue), nrdAmount: moneyInput(form.nrdAmount), paymentMethod: form.paymentMethod,
      });
      setConfirmOpen(false);
      const uploaded = await uploadReceipt(result.project);
      setCreated({ ...result, receiptPending: !uploaded });
    } catch (e) {
      setConfirmOpen(false);
      setSubmitError(errorMessage(e));
      // A slot taken meanwhile (MSG07) shows up after re-checking availability.
      halls.reload({ silent: true });
    } finally {
      setSaving(false);
    }
  };

  const retryUpload = async () => {
    setSaving(true);
    setSubmitError("");
    const uploaded = await uploadReceipt(created.project);
    setCreated((c) => ({ ...c, receiptPending: !uploaded }));
    setSaving(false);
  };

  return (
    <>
      <PageHeader code="M-03" title="Khởi tạo hợp đồng gốc" subtitle="Số hóa hợp đồng đã ký, giữ ca sảnh và cấp tài khoản cho khách hàng." />
      {created && !created.receiptPending && (
        <SuccessPanel project={created.project} email={form.customerEmail} temporaryPassword={created.temporaryPassword} emailStatus={created.emailStatus} />
      )}
      {submitError && <Alert severity="error" sx={{ mb: 2 }} role="alert">{submitError}</Alert>}
      {policy.error && <Alert severity="error" sx={{ mb: 2 }}>{policy.error}</Alert>}
      <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
        <Stack spacing={2}>
          <CustomerSection form={form} errors={errors} onChange={change} disabled={locked} />
          <ContractSection form={form} errors={errors} onChange={change} halls={hallList} hallsLoading={halls.loading} hallsError={halls.error} disabled={locked} />
          <NrdSection form={form} errors={errors} onChange={change} minimum={minimum} ratio={policy.data?.nrdRatio} disabled={locked && !created?.receiptPending} />
          <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
            <Button component={RouterLink} to={PATHS.manager.projects} disabled={saving}>Hủy</Button>
            {created?.receiptPending ? (
              <Button variant="contained" onClick={retryUpload} disabled={saving || !form.receipt}>Tải lên biên nhận</Button>
            ) : (
              <Button variant="contained" onClick={review} disabled={saving || locked}>Xác nhận & tạo dự án</Button>
            )}
          </Stack>
        </Stack>
        <SummaryCard form={form} minimum={minimum} />
      </Box>
      <ConfirmDialog open={confirmOpen} form={form} saving={saving} onClose={() => setConfirmOpen(false)} onConfirm={create} />
    </>
  );
}
