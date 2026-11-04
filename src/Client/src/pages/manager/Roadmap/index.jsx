import { useSearchParams } from "react-router-dom";
import { Alert, MenuItem, Stack, TextField, Typography } from "@mui/material";
import RoadmapTable from "./components/RoadmapTable";
import { useProject } from "../../../hooks/useProject";

// M-06, read-only: "Mark done" for Manager tasks waits for its API. Owner filter on the query string.
export default function Roadmap() {
  const { roadmap } = useProject();
  const [params, setParams] = useSearchParams();
  const owner = params.get("owner") || "";
  const tasks = roadmap.filter((t) => !owner || t.assigneeRole === owner);
  return (
    <>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, justifyContent: "space-between", alignItems: { sm: "center" } }}>
        <Typography variant="h6">Lộ trình và nhiệm vụ (M-06)</Typography>
        <TextField select size="small" label="Phụ trách" value={owner} sx={{ minWidth: 200 }}
          onChange={(e) => setParams(e.target.value ? { owner: e.target.value } : {}, { replace: true })}>
          <MenuItem value="">Tất cả</MenuItem>
          <MenuItem value="Manager">Nhiệm vụ của quản lý</MenuItem>
          <MenuItem value="Customer">Nhiệm vụ của khách hàng</MenuItem>
        </TextField>
      </Stack>
      {roadmap.length === 0 ? (
        <Alert severity="info">Lộ trình được tạo sau khi baseline được đóng băng.</Alert>
      ) : <RoadmapTable tasks={tasks} />}
    </>
  );
}
