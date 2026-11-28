import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Alert, Box, Button, Stack, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import EventIcon from "@mui/icons-material/Event";
import PostAddIcon from "@mui/icons-material/PostAdd";
import PageHeader from "../../../components/PageHeader";
import CustomerSection from "./components/CustomerSection";
import ContractSection from "./components/ContractSection";
import ServicesSection from "./components/ServicesSection";
import DepositSection from "./components/DepositSection";
import SummaryCard from "./components/SummaryCard";
import ConfirmDialog from "./components/ConfirmDialog";
import SuccessPanel from "./components/SuccessPanel";
import AddonStart from "./components/AddonStart";
import { bookingWindow, contractValue, requiredDeposit, validateProject } from "./validation";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { configurationService } from "../../../services/configurationService";
import { PATHS } from "../../../routes/paths";
import { errorMessage, moneyInput } from "../../../utils/format";

const EMPTY = { coupleName: "", customerEmail: "", customerPhone: "", hall: "", eventDate: "", slot: "", lines: [], depositAmount: "", paymentMethod: "Bank", receipt: null };

// Create a contract. First choose: a new event (stage 1: services, 15–90 days window, 30% deposit) or an addon contract
// for an existing locked event (existing customer, no deposit). Creating the event and uploading the receipt are two calls;
// when the upload fails the event exists, so the retry only re-sends the receipt.
export default function CreateProject() {
  const [kind, setKind] = useState("event");
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [created, setCreated] = useState(null);

  const policy = useAsync(() => configurationService.getPolicies());
  const catalog = useAsync(() => configurationService.getCatalog());
  const frames = useAsync(() => configurationService.getPriceFrames());
  const halls = useAsync(() => projectService.getHalls(form.eventDate || undefined), [form.eventDate]);
  const hallList = halls.data ?? [];
  const slots = hallList.find((h) => h.name === form.hall)?.slots ?? [];
  const minimum = requiredDeposit(form.lines, policy.data);
  const window = bookingWindow(policy.data);
  const locked = !!created;

  const change = (key, value) => setForm((f) => ({ ...f, [key]: value, ...(key === "hall" || key === "eventDate" ? { slot: "" } : {}) }));

  const review = () => {
    const next = validateProject(form, policy.data, slots, frames.data ?? []);
    setErrors(next);
    setSubmitError("");
    if (Object.keys(next).length) { setSubmitError("Vui lòng kiểm tra các trường được đánh dấu."); return; }
    setConfirmOpen(true);
  };

  const uploadReceipt = async (project) => {
    if (!form.receipt) return true;
    try {
      await projectService.uploadDepositReceipt(project.id, form.receipt);
      return true;
    } catch (e) {
      setSubmitError(`Sự kiện ${project.id} đã được tạo nhưng chưa lưu được biên nhận: ${errorMessage(e)} Vui lòng chọn lại tệp và tải lên.`);
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
        baseValue: contractValue(form.lines), depositAmount: moneyInput(form.depositAmount) || minimum, paymentMethod: form.paymentMethod,
        items: form.lines.map((l) => ({ catalogItemId: l.catalogItemId, description: l.description, quantity: Number(l.quantity), unitPrice: Number(l.unitPrice) })),
        // The policy shown on this form: the API refuses to sign if the Admin changed it meanwhile.
        policyStamp: policy.data?.stamp,
      });
      setConfirmOpen(false);
      const uploaded = await uploadReceipt(result.project);
      setCreated({ ...result, receiptPending: !uploaded });
    } catch (e) {
      setConfirmOpen(false);
      setSubmitError(errorMessage(e));
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
      <PageHeader title="Tạo hợp đồng" subtitle="Ký hợp đồng sự kiện mới hoặc tạo hợp đồng phát sinh cho sự kiện đã chốt." />
      <Stack spacing={1} sx={{ mb: 3 }}>
        <Typography variant="subtitle1">Loại hợp đồng</Typography>
        <ToggleButtonGroup exclusive color="primary" value={kind} onChange={(_, v) => v && !locked && setKind(v)} sx={{ flexWrap: "wrap" }}>
          <ToggleButton value="event" sx={{ px: 3 }}><EventIcon sx={{ mr: 1 }} />Sự kiện mới</ToggleButton>
          <ToggleButton value="addon" sx={{ px: 3 }}><PostAddIcon sx={{ mr: 1 }} />Phát sinh cho sự kiện đã có</ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      {kind === "addon" ? <AddonStart /> : (
        <>
          {created && !created.receiptPending && (
            <SuccessPanel project={created.project} email={form.customerEmail} temporaryPassword={created.temporaryPassword} emailStatus={created.emailStatus} />
          )}
          {submitError && <Alert severity="error" sx={{ mb: 2 }} role="alert">{submitError}</Alert>}
          {(policy.error || catalog.error) && <Alert severity="error" sx={{ mb: 2 }}>{policy.error || catalog.error}</Alert>}
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
            <Stack spacing={2}>
              <CustomerSection form={form} errors={errors} onChange={change} disabled={locked} />
              <ContractSection form={form} errors={errors} onChange={change} halls={hallList} hallsLoading={halls.loading} hallsError={halls.error}
                disabled={locked} window={window} />
              <ServicesSection lines={form.lines} catalog={catalog.data ?? []} frames={frames.data ?? []} error={errors.lines}
                onChange={(lines) => change("lines", lines)} disabled={locked} />
              <DepositSection form={form} errors={errors} onChange={change} minimum={minimum} ratio={policy.data?.depositRatio}
                disabled={locked && !created?.receiptPending} />
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button component={RouterLink} to={PATHS.manager.projects} disabled={saving}>Hủy</Button>
                {created?.receiptPending ? (
                  <Button variant="contained" onClick={retryUpload} disabled={saving || !form.receipt}>Tải lên biên nhận</Button>
                ) : (
                  <Button variant="contained" onClick={review} disabled={saving || locked}>Xác nhận & tạo sự kiện</Button>
                )}
              </Stack>
            </Stack>
            <SummaryCard form={form} minimum={minimum} policy={policy.data} />
          </Box>
          <ConfirmDialog open={confirmOpen} form={form} deposit={moneyInput(form.depositAmount) || minimum} saving={saving}
            onClose={() => setConfirmOpen(false)} onConfirm={create} />
        </>
      )}
    </>
  );
}
