import { Avatar, Card, CardContent, Chip, Divider, Stack, Typography } from "@mui/material";
import { initials, roleLabel } from "../../../../utils/format";

// G-02 fields are read-only: the venue changes profile data, not the user (SRS 1.2).
export default function ProfileDetails({ profile }) {
  const rows = [
    ["Họ và tên", profile.fullName],
    ["Email", profile.email],
    ["Số điện thoại", profile.phone || "Chưa cập nhật"],
    ["Vai trò", roleLabel(profile.role)],
  ];
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={2} sx={{ alignItems: "center", mb: 2 }}>
          <Avatar sx={{ bgcolor: "primary.main", width: 56, height: 56 }}>{initials(profile.fullName)}</Avatar>
          <div>
            <Typography variant="h6">{profile.fullName}</Typography>
            <Chip label={roleLabel(profile.role)} size="small" color="secondary" />
          </div>
        </Stack>
        <Divider sx={{ mb: 2 }} />
        <Stack spacing={1.5}>
          {rows.map(([label, value]) => (
            <Stack key={label} direction={{ xs: "column", sm: "row" }} spacing={{ sm: 2 }}>
              <Typography color="text.secondary" sx={{ width: { sm: 160 }, flexShrink: 0 }}>{label}</Typography>
              <Typography sx={{ wordBreak: "break-word" }}>{value}</Typography>
            </Stack>
          ))}
        </Stack>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          Thông tin cá nhân do trung tâm quản lý. Vui lòng liên hệ trung tâm nếu cần thay đổi.
        </Typography>
      </CardContent>
    </Card>
  );
}
