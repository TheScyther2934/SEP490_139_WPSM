import { useState } from "react";
import { useDispatch } from "react-redux";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ExceptionTable from "./components/ExceptionTable";
import ExceptionDetail from "./components/ExceptionDetail";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-18
export default function ExceptionRequests() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => adminService.getExceptionRequests());
  const [selected, setSelected] = useState(null);

  const decide = async (approve, note) => {
    try {
      await adminService.decideException(selected.id, approve, note);
    } catch (e) {
      return errorMessage(e);
    }
    dispatch(notify(`Đã ${approve ? "chấp thuận" : "từ chối"} yêu cầu ${selected.code}.`));
    setSelected(null);
    reload();
    return null;
  };

  return (
    <>
      <PageHeader code="A-18" title="Yêu cầu ngoại lệ" subtitle="Đề xuất ngoài khung giá hoặc chính sách cần Ban điều hành quyết định." />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}>
        {data && <ExceptionTable requests={data} selectedId={selected?.id} onOpen={setSelected} />}
      </DataState>
      {selected && <ExceptionDetail key={selected.id} request={selected} onDecide={decide} />}
    </>
  );
}
