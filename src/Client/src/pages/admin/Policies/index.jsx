import { useState } from "react";
import { useDispatch } from "react-redux";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PolicyCards from "./components/PolicyCards";
import PolicyDialog from "./components/PolicyDialog";
import { useAsync } from "../../../hooks/useAsync";
import { configurationService } from "../../../services/configurationService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// Event rules: deposit, booking window, lock and supplier deadlines, decrease limit, settlement due date, hotline.
export default function Policies() {
  const dispatch = useDispatch();
  const { data: policy, loading, error, reload } = useAsync(() => configurationService.getPolicies());
  const [open, setOpen] = useState(false);
  const save = async (body) => {
    try { await configurationService.updatePolicies(body); } catch (e) { return errorMessage(e); }
    setOpen(false);
    dispatch(notify("Lưu thành công."));
    reload({ silent: true });
    return null;
  };
  return (
    <>
      <PageHeader title="Chính sách của trung tâm" subtitle="Thay đổi chỉ áp dụng cho hợp đồng ký từ ngày hiệu lực. Sự kiện đã ký giữ nguyên điều khoản lúc ký." />
      <DataState loading={loading} error={error} onRetry={reload}>
        {policy && <PolicyCards policy={policy} onEdit={() => setOpen(true)} />}
      </DataState>
      <PolicyDialog open={open} policy={policy} onClose={() => setOpen(false)} onSave={save} />
    </>
  );
}
