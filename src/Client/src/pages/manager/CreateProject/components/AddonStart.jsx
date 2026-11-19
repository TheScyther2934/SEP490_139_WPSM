import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Alert, Button, Card, CardContent, Chip, InputAdornment, List, ListItemButton, ListItemText, Stack, TextField, Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import DataState from "../../../../components/DataState";
import StatusChip from "../../../../components/StatusChip";
import { useAsync } from "../../../../hooks/useAsync";
import { eventFlowService } from "../../../../services/eventFlowService";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

// "Addon contract" choice: find an existing customer (no new account, no duplicate check), pick one of their locked events,
// then create the addon contract on that event's page. No deposit is taken for an addon.
export default function AddonStart() {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [term, setTerm] = useState("");
  useEffect(() => { const id = setTimeout(() => setTerm(query.trim()), 350); return () => clearTimeout(id); }, [query]);
  const customers = useAsync(() => eventFlowService.lookupCustomers(term), [term]);
  const [selected, setSelected] = useState(null);

  return (
    <Card>
      <CardContent>
        <Typography variant="h6">Hợp đồng phát sinh cho sự kiện đã có</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Dùng khi khách đã có sự kiện đã chốt dịch vụ và cần thêm dịch vụ. Không đặt cọc; khách phải bấm xác nhận rồi mới thực hiện và cộng vào hóa đơn tất toán.
        </Typography>
        <TextField fullWidth label="Tìm khách theo tên, email hoặc số điện thoại" value={query} onChange={(e) => setQuery(e.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} sx={{ mb: 2 }} />
        <DataState loading={customers.loading} error={customers.error} empty={!customers.data?.length} emptyText="Không tìm thấy khách hàng.">
          <List dense sx={{ maxHeight: 360, overflow: "auto", border: 1, borderColor: "divider", borderRadius: 1 }}>
            {customers.data?.map((c) => (
              <ListItemButton key={c.userId} selected={selected?.userId === c.userId} onClick={() => setSelected(c)}>
                <ListItemText primary={c.fullName} secondary={`${c.email}${c.phone ? ` · ${c.phone}` : ""} · ${c.events.length} sự kiện`} />
              </ListItemButton>
            ))}
          </List>
        </DataState>
        {selected && (
          <Stack spacing={1} sx={{ mt: 2 }}>
            <Typography variant="subtitle1">Sự kiện của {selected.fullName}</Typography>
            {selected.events.length === 0 && <Alert severity="info">Khách chưa có sự kiện nào do bạn phụ trách.</Alert>}
            {selected.events.map((e) => (
              <Stack key={e.eventCode} direction={{ xs: "column", sm: "row" }} spacing={1}
                sx={{ p: 1.5, border: 1, borderColor: "divider", borderRadius: 1, alignItems: { sm: "center" }, justifyContent: "space-between" }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                  <Typography fontWeight={600}>{e.eventCode}</Typography>
                  <StatusChip kind="project" status={e.status} />
                  <Typography variant="body2" color="text.secondary">{e.hall} · {dateVi(e.eventDate)}</Typography>
                </Stack>
                {e.canAddAddon
                  ? <Button variant="contained" onClick={() => navigate(`${PATHS.manager.addons(e.eventCode)}?new=1`)}>Tạo phát sinh</Button>
                  : <Chip size="small" label={e.status === "Adjusting" ? "Còn điều chỉnh trực tiếp" : "Không thể thêm phát sinh"} />}
              </Stack>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
