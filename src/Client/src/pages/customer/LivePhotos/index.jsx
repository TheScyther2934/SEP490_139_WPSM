import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PhotoGallery from "./components/PhotoGallery";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";

// C-11: photos come from the dashboard API (latest proof photo, signed URL), refreshed every 30 s.
export default function LivePhotos() {
  const { data, loading, error, reload } = useAsync(() => customerService.getDashboard());
  usePolling(() => reload({ silent: true }));
  const photos = data?.latestProofPhoto ? [data.latestProofPhoto] : [];
  return (
    <>
      <PageHeader code="C-11" title="Ảnh thực tế hôn lễ" subtitle="Ảnh do Ban điều phối tải lên trong ngày sự kiện." />
      <DataState loading={loading} error={error} onRetry={reload}>
        <PhotoGallery photos={photos} />
      </DataState>
    </>
  );
}
