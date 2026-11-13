import { useState } from "react";
import { useDispatch } from "react-redux";
import { Button } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import { fileService } from "../services/fileService";
import { notify } from "../store/notifySlice";
import { errorMessage } from "../utils/format";

// Opens a stored file through a short-lived signed URL (API-02); the API checks project access first (GBR-12).
export default function FileButton({ fileId, children = "Tải xuống", ...props }) {
  const dispatch = useDispatch();
  const [busy, setBusy] = useState(false);
  const open = async () => {
    setBusy(true);
    try {
      const signed = await fileService.getSignedUrl(fileId);
      window.location.assign(signed.url);
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setBusy(false);
    }
  };
  return <Button size="small" startIcon={<DownloadIcon />} onClick={open} disabled={!fileId || busy} {...props}>{children}</Button>;
}
