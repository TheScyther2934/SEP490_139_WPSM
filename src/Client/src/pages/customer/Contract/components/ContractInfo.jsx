import { Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import { countdownLabel, dateTimeVi, dateVi, money, slotLabel } from "../../../../utils/format";

const NRD_STATUS = { Paid: "Đã xác nhận thanh toán", Due: "Chờ xác nhận thanh toán", Upcoming: "Chưa đến hạn", Unknown: "Chưa có trạng thái thanh toán" };

export default function ContractInfo({ project }) {
  const frozen = !!project.baselineFrozenAt;
  const facts = [
    ["Sảnh tiệc", project.hall],
    ["Ca tiệc", `Ca ${slotLabel(project.slot)}${project.slotTime ? ` · ${project.slotTime}` : ""}`],
    ["Ngày tổ chức", `${dateVi(project.eventDate)} · ${countdownLabel(project.eventDate)}`],
    ["Giá trị hợp đồng gốc", money(project.baseValue)],
    ["Đặt cọc NRD", `${money(project.nrdAmount)} · ${NRD_STATUS[project.nrdStatus] ?? NRD_STATUS.Unknown}`],
  ];
  return (
    <Card sx={{ mb: 2 }}>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1, mb: 2 }}>
          <Typography variant="h6">Hợp đồng {project.id}</Typography>
          <Chip size="small" color={frozen ? "success" : "warning"} label={frozen ? "Baseline v1 · Đã đóng băng" : "Đang soạn đặc tả"} />
        </Stack>
        <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "1fr 1fr", lg: "repeat(5, 1fr)" } }}>
          {facts.map(([label, value]) => (
            <div key={label}>
              <Typography variant="caption" color="text.secondary">{label}</Typography>
              <Typography fontWeight={600}>{value}</Typography>
            </div>
          ))}
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          {frozen
            ? `Đặc tả gốc đã đóng băng lúc ${dateTimeVi(project.baselineFrozenAt)}, hiển thị chỉ đọc để đối chiếu với hợp đồng.`
            : "Quản lý đang hoàn thiện đặc tả hợp đồng. Các hạng mục hiện có chỉ để bạn theo dõi."}
        </Typography>
      </CardContent>
    </Card>
  );
}
