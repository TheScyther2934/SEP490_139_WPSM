import { useCallback, useState } from "react";
import TaskCard from "./TaskCard";
import TaskDialog from "./TaskDialog";
import ProofUploadDialog from "./ProofUploadDialog";
import PhotoViewer from "./PhotoViewer";
import TextDialog from "../TextDialog";
import { useAsync } from "../../hooks/useAsync";
import { useAction } from "../../hooks/useAction";
import { dayOfService } from "../../services/operationsService";

// Coordination tasks of one event and everything done with them: complete with proof, add proof, add / edit / delete a task,
// generate the tasks from the locked services, view photos. Shared by the department sheets and the event-day board.
export function useCoordinationTasks(code) {
  const board = useAsync(() => dayOfService.get(code), [code]);
  const { busy, run } = useAction();
  const [proof, setProof] = useState(null);        // { task, mode: "complete" | "add" }
  const [editing, setEditing] = useState(undefined); // undefined = closed, null = new, task = edit
  const [newDepartment, setNewDepartment] = useState("Decor");
  const [deleting, setDeleting] = useState(null);
  const [viewer, setViewer] = useState(null);       // { title, photos, index }
  const data = board.data;

  const refresh = (result) => { if (result) board.setData(result === true ? board.data : result); return result; };

  const submitProof = async (files, note) => {
    const { task, mode } = proof;
    const result = mode === "add"
      ? await run(() => dayOfService.addPhotos(code, task.taskId, files), `Đã tải ${files.length} minh chứng cho "${task.taskName}".`)
      : await run(() => dayOfService.complete(code, task.taskId, files, note), `Đã hoàn thành "${task.taskName}".`);
    if (refresh(result)) setProof(null);
  };
  const saveTask = async (form) => {
    const result = editing
      ? await run(() => dayOfService.updateTask(code, editing.taskId, form), "Đã lưu nhiệm vụ.")
      : await run(() => dayOfService.createTask(code, form), `Đã thêm nhiệm vụ "${form.taskName}".`);
    if (refresh(result)) setEditing(undefined);
  };
  const deleteTask = async () => {
    if (refresh(await run(() => dayOfService.deleteTask(code, deleting.taskId), "Đã xóa nhiệm vụ."))) setDeleting(null);
  };
  const generate = async () => refresh(await run(() => dayOfService.load(code), "Đã tạo nhiệm vụ điều phối từ các dịch vụ đã khóa."));
  const addTask = (department) => { setNewDepartment(department || "Decor"); setEditing(null); };
  const moveViewer = useCallback((index) => setViewer((v) => (v ? { ...v, index } : v)), []);

  // One task card wired to the dialogs.
  const renderTask = (task, options = {}) => (
    <TaskCard key={task.taskId} task={task} canAct={data?.canAddPhotos} maxPhotos={data?.maxPhotosPerTask ?? 6} {...options}
      onComplete={(t) => setProof({ task: t, mode: "complete" })}
      onAddPhotos={(t) => setProof({ task: t, mode: "add" })}
      onOpenPhoto={(t, index) => setViewer({ title: t.taskName, photos: t.photos.filter((p) => p.isImage), index })}
      onEdit={setEditing} onDelete={setDeleting} />
  );

  const dialogs = (
    <>
      <ProofUploadDialog task={proof?.task} mode={proof?.mode} maxPhotos={data?.maxPhotosPerTask ?? 6} busy={busy}
        onClose={() => setProof(null)} onSubmit={submitProof} />
      <TaskDialog task={editing} department={newDepartment} eventDate={data?.eventDate} busy={busy} onClose={() => setEditing(undefined)} onSave={saveTask} />
      <TextDialog open={!!deleting} title="Xóa nhiệm vụ" color="error" confirmText="Xóa" busy={busy}
        description={`Xóa nhiệm vụ "${deleting?.taskName ?? ""}"? Thao tác không hoàn tác được.`}
        onClose={() => setDeleting(null)} onConfirm={deleteTask} />
      <PhotoViewer viewer={viewer} onClose={() => setViewer(null)} onMove={moveViewer} />
    </>
  );

  return { board, data, tasks: data?.tasks ?? [], busy, generate, addTask, renderTask, dialogs };
}
