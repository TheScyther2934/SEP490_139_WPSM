import { useState } from "react";
import { Button, Card, CardContent, Rating, Stack, TextField, Typography } from "@mui/material";

export const MODULES = [
  { value: "Hall", label: "Sảnh tiệc" },
  { value: "Menu", label: "Thực đơn" },
  { value: "Decor", label: "Trang trí sân khấu" },
  { value: "MC", label: "MC / chương trình" },
];

// C-13: 1–5 stars per module, optional comment up to 500 characters; rated modules are read-only (GBR-10).
export default function RatingForm({ existing, disabled, busy, onSubmit }) {
  const [values, setValues] = useState({});
  const [error, setError] = useState("");
  const rated = Object.fromEntries(existing.map((r) => [r.module, r]));
  const set = (module, patch) => setValues((v) => ({ ...v, [module]: { ...v[module], ...patch } }));
  const submit = () => {
    const ratings = MODULES.filter((m) => !rated[m.value] && values[m.value]?.stars)
      .map((m) => ({ module: m.value, stars: values[m.value].stars, comment: values[m.value].comment?.trim() || null }));
    if (ratings.length === 0) return setError("Hãy chấm sao ít nhất một hạng mục.");
    if (ratings.some((r) => r.comment?.length > 500)) return setError("Vượt quá độ dài tối đa 500 ký tự.");
    setError("");
    onSubmit(ratings);
  };
  return (
    <Stack spacing={2} id="ratingForm">
      {MODULES.map((m) => {
        const done = rated[m.value];
        const comment = values[m.value]?.comment ?? "";
        return (
          <Card key={m.value} data-module={m.value}>
            <CardContent>
              <Typography fontWeight={600}>{m.label}</Typography>
              <Rating value={done?.stars ?? values[m.value]?.stars ?? null} readOnly={!!done || disabled}
                onChange={(_, stars) => set(m.value, { stars })} sx={{ my: 1 }} />
              {done ? (
                <Typography color="text.secondary">{done.comment || "Không có nhận xét."} (đã gửi, không sửa được)</Typography>
              ) : (
                <TextField fullWidth multiline minRows={2} label="Nhận xét (không bắt buộc)" value={comment} disabled={disabled}
                  onChange={(e) => set(m.value, { comment: e.target.value })} error={comment.length > 500}
                  helperText={comment.length > 500 ? "Vượt quá độ dài tối đa 500 ký tự." : `${comment.length}/500`} />
              )}
            </CardContent>
          </Card>
        );
      })}
      {error && <Typography color="error">{error}</Typography>}
      <div><Button variant="contained" onClick={submit} disabled={disabled || busy || existing.length >= MODULES.length}>Gửi đánh giá</Button></div>
    </Stack>
  );
}
