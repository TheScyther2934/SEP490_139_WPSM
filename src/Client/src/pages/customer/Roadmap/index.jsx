import { useState } from "react";
import { useDispatch } from "react-redux";
import { Alert, Box, Stack } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ProgressSidebar from "./components/ProgressSidebar";
import MilestoneGroup from "./components/MilestoneGroup";
import TaskDialog from "./components/TaskDialog";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// Replaces one task in the roadmap and recounts the done totals.
const withTask = (roadmap, updated) => {
  const milestoneGroups = roadmap.milestoneGroups.map((g) => {
    const tasks = g.tasks.map((t) => (t.taskId === updated.taskId ? updated : t));
    return { ...g, tasks, doneCount: tasks.filter((t) => t.status === "Done").length };
  });
  return { ...roadmap, milestoneGroups, doneTasks: milestoneGroups.reduce((s, g) => s + g.doneCount, 0) };
};

// C-03 with the C-04 task dialog.
export default function Roadmap() {
  const dispatch = useDispatch();
  const { data, loading, error, reload, setData } = useAsync(() => customerService.getRoadmap());
  const [openTask, setOpenTask] = useState(null);

  // Returns an error message for the dialog, or nothing on success.
  const complete = async (task) => {
    try {
      const result = await customerService.completeTask(data.projectCode, task.taskId);
      if (!result.success) return result.message;
      setData((roadmap) => withTask(roadmap, result.updatedTask));
      setOpenTask(result.updatedTask);
      dispatch(notify("Đã xác nhận hoàn thành nhiệm vụ."));
      return null;
    } catch (e) {
      return errorMessage(e);
    }
  };

  const empty = data && data.milestoneGroups.every((g) => g.totalCount === 0);
  return (
    <>
      <PageHeader code="C-03" title="Lộ trình chuẩn bị" subtitle={data ? data.coupleName : undefined} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data === null ? <Alert severity="info">Chưa có dự án hôn lễ nào được gắn với tài khoản.</Alert>
          : empty ? <Alert severity="info">Lộ trình sẽ được tạo sau khi quản lý chốt baseline.</Alert>
          : data && (
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" }, alignItems: "start" }}>
              <Stack spacing={1}>
                {data.milestoneGroups.map((g) => <MilestoneGroup key={g.milestone} group={g} onOpenTask={setOpenTask} />)}
              </Stack>
              <ProgressSidebar roadmap={data} />
            </Box>
          )}
      </DataState>
      <TaskDialog task={openTask} onClose={() => setOpenTask(null)} onComplete={complete} />
    </>
  );
}
