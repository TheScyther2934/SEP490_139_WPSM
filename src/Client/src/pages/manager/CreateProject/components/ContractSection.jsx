import { Box, Card, CardContent, FormControlLabel, FormHelperText, MenuItem, Radio, RadioGroup, Stack, TextField, Typography } from "@mui/material";
import { dateVi, money, slotLabel, weekdayVi } from "../../../../utils/format";

// Hall, date and slot. The date picker only offers the booking window (15–90 days ahead); slot availability comes from GET /halls?date=.
export default function ContractSection({ form, errors, onChange, halls, hallsLoading, hallsError, disabled, window }) {
  const hall = halls.find((h) => h.name === form.hall);
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">2. Sảnh, ngày cưới và ca tiệc</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Chỉ ký hợp đồng khi ngày cưới cách hôm nay từ {dateVi(window.min)} đến {dateVi(window.max)} (không đặt quá sớm hay quá muộn).
        </Typography>
        <Stack spacing={2}>
          <TextField id="eventDate" label="Ngày cưới" type="date" value={form.eventDate} disabled={disabled}
            onChange={(e) => onChange("eventDate", e.target.value)} error={!!errors.eventDate}
            helperText={errors.eventDate ?? weekdayVi(form.eventDate)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: window.min, max: window.max } }} />
          <TextField id="hallSelect" select label="Sảnh tiệc" value={form.hall} disabled={disabled || hallsLoading}
            onChange={(e) => onChange("hall", e.target.value)} error={!!errors.hall || !!hallsError}
            helperText={errors.hall ?? hallsError ?? (hallsLoading ? "Đang kiểm tra lịch sảnh…" : "")}>
            {halls.map((h) => (
              <MenuItem key={h.hallId} value={h.name}>{h.name} – {h.minCapacity}~{h.maxCapacity} khách – {money(h.basePrice)}</MenuItem>
            ))}
          </TextField>
          <Box>
            <Typography variant="body2" color="text.secondary" gutterBottom>Ca tiệc</Typography>
            {hall && form.eventDate ? (
              <RadioGroup row value={form.slot} onChange={(e) => onChange("slot", e.target.value)}>
                {hall.slots.map((s) => (
                  <FormControlLabel key={s.slotId} value={s.slotName} disabled={disabled || !s.available} control={<Radio />}
                    label={`${slotLabel(s.slotName)} (${s.startTime.slice(0, 5)} – ${s.endTime.slice(0, 5)}) · ${s.available ? "Còn trống" : "Đã đặt"}`} />
                ))}
              </RadioGroup>
            ) : (
              <Typography variant="body2" color="text.secondary">Chọn sảnh và ngày cưới để xem ca còn trống.</Typography>
            )}
            {errors.slot && <FormHelperText error>{errors.slot}</FormHelperText>}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
