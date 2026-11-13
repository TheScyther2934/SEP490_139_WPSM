import { useState } from "react";
import { useDispatch } from "react-redux";
import { Box, Card, InputAdornment, Stack, TextField, Typography } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PriceFrameTable from "./components/PriceFrameTable";
import PriceFrameDialog from "./components/PriceFrameDialog";
import PriceHistoryDialog from "./components/PriceHistoryDialog";
import { useAsync } from "../../../hooks/useAsync";
import { configurationService } from "../../../services/configurationService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-04 with the A-05 dialog and the price history (UC-38). A new version starts on the effective date; the list shows the version in force today.
export default function PriceFrames() {
  const dispatch = useDispatch();
  const { data, loading, error, reload } = useAsync(() => configurationService.getPriceFrames());
  const [editing, setEditing] = useState(null);
  const [history, setHistory] = useState(null);
  const [query, setQuery] = useState("");

  const save = async (frame, body) => {
    try {
      await configurationService.updatePriceFrame(frame.id, body);
    } catch (e) {
      return errorMessage(e);
    }
    setEditing(null);
    dispatch(notify(`Đã lưu khung giá ${frame.serviceName}.`));
    reload({ silent: true });
    return null;
  };

  const q = query.trim().toLowerCase();
  const frames = (data ?? []).filter((f) => !q || f.serviceName.toLowerCase().includes(q) || f.variant.toLowerCase().includes(q));

  return (
    <>
      <PageHeader code="A-04" title="Khung giá (PriceFrame)" subtitle="Biên độ giá sàn và trần cho từng dịch vụ." />
      <Card>
        <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2, borderBottom: 1, borderColor: "divider", alignItems: { md: "center" } }}>
          <TextField size="small" placeholder="Tìm theo tên dịch vụ" value={query} onChange={(e) => setQuery(e.target.value)} sx={{ width: { md: 300 } }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }, htmlInput: { "aria-label": "Tìm dịch vụ" } }} />
          <Box sx={{ flex: 1 }} />
          <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 560 }}>
            Đơn giá trong đặc tả và yêu cầu thay đổi bắt buộc nằm trong khung giá đang hiệu lực. Đề xuất ngoài khung cần Ban điều hành duyệt ngoại lệ.
          </Typography>
        </Stack>
        <DataState loading={loading} error={error} onRetry={reload}>
          {data && <PriceFrameTable frames={frames} onEdit={setEditing} onHistory={setHistory} />}
        </DataState>
      </Card>
      <PriceFrameDialog frame={editing} onClose={() => setEditing(null)} onSave={save} />
      <PriceHistoryDialog frame={history} onClose={() => setHistory(null)} />
    </>
  );
}
