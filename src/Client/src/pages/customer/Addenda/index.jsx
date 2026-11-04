import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import { Button, Stack } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import AddendumCard from "./components/AddendumCard";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { fileService } from "../../../services/fileService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { errorMessage } from "../../../utils/format";

// C-09: addenda created from approved change requests, read-only. Downloads go through a signed URL (API-02).
export default function Addenda() {
  const dispatch = useDispatch();
  // 404 = the account has no project yet, shown as "no addenda".
  const { data, loading, error, reload } = useAsync(() => projectService.getAddenda()
    .catch((e) => { if (e.response?.status === 404) return []; throw e; }));
  const [downloading, setDownloading] = useState(false);

  const download = async (fileId) => {
    setDownloading(true);
    try {
      const signed = await fileService.getSignedUrl(fileId);
      window.location.assign(signed.url);
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setDownloading(false);
    }
  };

  return (
    <>
      <PageHeader code="C-09" title="Phụ lục hợp đồng" subtitle="Phụ lục phát sinh từ yêu cầu thay đổi đã được hai bên phê duyệt."
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.contract}>Về hợp đồng</Button>} />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length} emptyText="Chưa có phụ lục hợp đồng.">
        <Stack spacing={2} id="addendaList">
          {data?.map((a) => <AddendumCard key={a.code} addendum={a} onDownload={download} downloading={downloading} />)}
        </Stack>
      </DataState>
    </>
  );
}
