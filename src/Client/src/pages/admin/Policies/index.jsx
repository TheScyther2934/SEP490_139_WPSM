import { useState } from "react";
import { useDispatch } from "react-redux";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PolicyCards from "./components/PolicyCards";
import PolicyDialog from "./components/PolicyDialog";
import PenaltyRulesDialog from "./components/PenaltyRulesDialog";
import { useAsync } from "../../../hooks/useAsync";
import { configurationService } from "../../../services/configurationService";
import { configurationAdminService } from "../../../services/operationsService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-06 with the A-07 ratio dialog (UC-39) and the A-08 T-day penalty table (UC-40).
export default function Policies() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => Promise.all([configurationService.getPolicies(), configurationAdminService.penaltyRules()]));
  const [policy, rules] = data ?? [];
  const [dialog, setDialog] = useState(null);

  const saved = () => {
    setDialog(null);
    dispatch(notify("Lưu thành công."));
    reload({ silent: true });
    return null;
  };
  const saveDeposit = async (body) => {
    try { await configurationService.updatePolicies(body); } catch (e) { return errorMessage(e); }
    return saved();
  };
  const savePenalty = async (list, effectiveFrom) => {
    try { await configurationAdminService.updatePenaltyRules(list, effectiveFrom); } catch (e) { return errorMessage(e); }
    return saved();
  };

  return (
    <>
      <PageHeader code="A-06" title="Chính sách của trung tâm" subtitle="Thay đổi chỉ áp dụng từ ngày hiệu lực, không ảnh hưởng hợp đồng gốc và phụ lục đã tạo trước đó." />
      <DataState loading={loading} error={error} onRetry={reload}>
        {policy && <PolicyCards policy={policy} rules={rules} onEditDeposit={() => setDialog("deposit")} onEditPenalty={() => setDialog("penalty")} />}
      </DataState>
      <PolicyDialog kind={dialog === "deposit" ? "deposit" : null} policy={policy} onClose={() => setDialog(null)} onSave={saveDeposit} />
      <PenaltyRulesDialog open={dialog === "penalty"} rules={rules ?? []} onClose={() => setDialog(null)} onSave={savePenalty} />
    </>
  );
}
