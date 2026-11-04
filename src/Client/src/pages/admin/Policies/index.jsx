import { useState } from "react";
import { useDispatch } from "react-redux";
import { Alert } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PolicyCards from "./components/PolicyCards";
import PolicyDialog from "./components/PolicyDialog";
import { useAsync } from "../../../hooks/useAsync";
import { configurationService } from "../../../services/configurationService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-06 with the A-07 and A-08 dialogs.
export default function Policies() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => configurationService.getPolicies());
  const [dialog, setDialog] = useState(null);

  const save = async (policy) => {
    try {
      await configurationService.updatePolicies(policy);
    } catch (e) {
      return errorMessage(e);
    }
    setDialog(null);
    dispatch(notify("Lưu thành công."));
    reload({ silent: true });
    return null;
  };

  return (
    <>
      <PageHeader code="A-06" title="Chính sách của trung tâm" subtitle="Tỷ lệ đặt cọc, mốc thanh toán và phạt yêu cầu thay đổi." />
      <Alert severity="info" sx={{ mb: 2 }}>
        Thay đổi chính sách chỉ áp dụng từ ngày hiệu lực, không ảnh hưởng hợp đồng gốc và phụ lục đã tạo trước đó.
      </Alert>
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && <PolicyCards policy={data} onEditDeposit={() => setDialog("deposit")} onEditPenalty={() => setDialog("penalty")} />}
      </DataState>
      <PolicyDialog kind={dialog} policy={data} onClose={() => setDialog(null)} onSave={save} />
    </>
  );
}
