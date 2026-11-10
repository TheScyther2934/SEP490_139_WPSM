import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PhotoGallery from "./components/PhotoGallery";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { dayOfService } from "../../../services/operationsService";

// C-11 (UC-13): every proof photo of the customer's project, newest first, refreshed every 30 s (signed URLs).
export default function LivePhotos() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const { data, loading, error, reload } = useAsync(() => (project ? dayOfService.photos(project.id) : Promise.resolve([])), [project?.id]);
  usePolling(() => reload({ silent: true }));
  return (
    <>
      <PageHeader code="C-11" title="Ảnh thực tế hôn lễ" subtitle="Ảnh do Ban điều phối tải lên trong ngày sự kiện, tự làm mới mỗi 30 giây." />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload}>
        <PhotoGallery photos={data ?? []} />
      </DataState>
    </>
  );
}
