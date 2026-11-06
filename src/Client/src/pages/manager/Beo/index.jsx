import { useState } from "react";
import { Stack, Typography } from "@mui/material";
import DataState from "../../../components/DataState";
import TextDialog from "../../../components/TextDialog";
import BeoHeader from "./components/BeoHeader";
import BeoContent from "./components/BeoContent";
import ReapproveDialog from "./components/ReapproveDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { beoService } from "../../../services/operationsService";

// M-10 (UC-27, UC-28) and the M-12 re-approve dialog (UC-29).
export default function Beo() {
  const { project } = useProject();
  const { data: beo, loading, error, reload, setData } = useAsync(() => beoService.get(project.id), [project.id]);
  const { busy, run } = useAction();
  const [dialog, setDialog] = useState(null);
  const act = async (call, message) => {
    const updated = await run(call, message);
    if (updated) { setData(updated); setDialog(null); }
  };
  return (
    <>
      <Typography variant="h6" sx={{ mb: 2 }}>BEO – lệnh tổ chức sự kiện (M-10)</Typography>
      <DataState loading={loading} error={error} onRetry={reload}>
        {beo && (
          <Stack spacing={2}>
            <BeoHeader projectCode={project.id} beo={beo} busy={busy} onFreeze={() => setDialog("freeze")} onReapprove={() => setDialog("reapprove")}
              onSaveNotes={(notes) => act(() => beoService.saveNotes(project.id, notes), "Lưu thành công (MSG03).")} />
            <BeoContent beo={beo} />
          </Stack>
        )}
      </DataState>
      <TextDialog open={dialog === "freeze"} title="Duyệt và đóng băng BEO" busy={busy} confirmText="Đóng băng"
        description="Đóng băng không thể hoàn tác. Hệ thống tạo 4 file PDF cho Bếp, Phục vụ, Trang trí, Kỹ thuật/MC. Bạn có muốn tiếp tục? (MSG18)"
        onClose={() => setDialog(null)} onConfirm={() => act(() => beoService.freeze(project.id), "Đã đóng băng BEO và tạo file PDF các bộ phận.")} />
      {beo && (
        <ReapproveDialog open={dialog === "reapprove"} beo={beo} busy={busy} onClose={() => setDialog(null)}
          onConfirm={() => act(() => beoService.reapprove(project.id), "Đã tạo phiên bản BEO mới.")} />
      )}
    </>
  );
}
