import { useState } from "react";
import { useDispatch } from "react-redux";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ComplaintTable from "./components/ComplaintTable";
import ComplaintDetail from "./components/ComplaintDetail";
import ResolveDialog from "./components/ResolveDialog";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-16 with the A-17 dialog.
export default function EscalatedComplaints() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => adminService.getEscalatedComplaints());
  const [selected, setSelected] = useState(null);
  const [pendingDecision, setPendingDecision] = useState(null);

  // Returns an error message for the dialog, or nothing on success.
  const resolve = async (decision) => {
    try {
      await adminService.resolveComplaint(selected.complaintId, decision);
    } catch (e) {
      return errorMessage(e);
    }
    dispatch(notify(`Đã giải quyết khiếu nại ${selected.code}.`));
    setPendingDecision(null);
    setSelected(null);
    reload();
    return null;
  };

  return (
    <>
      <PageHeader code="A-16" title="Khiếu nại chuyển cấp" subtitle="Khiếu nại quản lý đã chuyển lên Ban điều hành." />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}>
        {data && <ComplaintTable complaints={data} selectedId={selected?.complaintId} onOpen={setSelected} />}
      </DataState>
      {selected && <ComplaintDetail key={selected.complaintId} complaint={selected} onResolve={setPendingDecision} />}
      <ResolveDialog complaint={selected} initialDecision={pendingDecision ?? ""} open={pendingDecision != null}
        onClose={() => setPendingDecision(null)} onConfirm={resolve} />
    </>
  );
}
