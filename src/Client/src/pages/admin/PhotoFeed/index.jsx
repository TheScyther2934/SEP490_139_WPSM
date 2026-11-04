import { Box } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PhotoCard from "../../../components/PhotoCard";
import FeedFilters from "./components/FeedFilters";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { PATHS } from "../../../routes/paths";
import { isoDate } from "../../../utils/format";

const unique = (values) => [...new Set(values.filter(Boolean))].sort();

// A-13: photos of one day, filtered on the client; refreshed every 30 s.
export default function PhotoFeed() {
  const [params, setParams] = useSearchParams();
  const filters = {
    date: params.get("date") || isoDate(new Date()),
    projectCode: params.get("projectCode") || "",
    hall: params.get("hall") || "",
    status: params.get("status") || "",
  };
  const { data, loading, error, reload } = useAsync(() => adminService.getPhotos(filters.date), [filters.date]);
  usePolling(() => reload({ silent: true }));

  const change = (key, value) => {
    const next = { ...filters, [key]: value };
    setParams(Object.fromEntries(Object.entries(next).filter(([, v]) => v)), { replace: true });
  };

  const photos = data ?? [];
  // Keep the selected project in the list even when that day has no photo for it (link from A-01/A-15).
  const projects = unique([...photos.map((p) => p.projectCode), filters.projectCode]);
  const halls = unique([...photos.map((p) => p.hallName), filters.hall]);
  const shown = photos.filter((p) => (!filters.projectCode || p.projectCode === filters.projectCode)
    && (!filters.hall || p.hallName === filters.hall) && (!filters.status || p.taskStatus === filters.status));

  return (
    <>
      <PageHeader code="A-13" title="Ảnh minh chứng" subtitle="Ảnh do điều phối viên tải lên trong ngày sự kiện." />
      <FeedFilters filters={filters} projects={projects} halls={halls} onChange={change} />
      <DataState loading={loading} error={error} onRetry={reload} empty={!shown.length}>
        <Box id="a13Photos" sx={{ display: "grid", gap: 2, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
          {shown.map((p) => <PhotoCard key={p.photoId} photo={p} height={180} to={PATHS.admin.photo(p.photoId)} />)}
        </Box>
      </DataState>
    </>
  );
}
