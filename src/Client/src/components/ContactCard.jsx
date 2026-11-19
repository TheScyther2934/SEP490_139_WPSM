import { Alert, Avatar, Card, CardContent, Link, List, ListItem, ListItemAvatar, ListItemText, Stack, Typography } from "@mui/material";
import PhoneIcon from "@mui/icons-material/Phone";
import SupportAgentIcon from "@mui/icons-material/SupportAgent";
import { initials } from "../utils/format";

// Who the customer contacts to change anything: every operator of the event and the hotline.
// Shown to customers on the overview and the booked services page.
export default function ContactCard({ contacts = [], hotline, locked }) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
          <SupportAgentIcon color="primary" />
          <Typography variant="h6">Người phụ trách sự kiện</Typography>
        </Stack>
        <Alert severity="info" sx={{ mb: 1 }}>
          {locked
            ? "Dịch vụ đã chốt, bạn chỉ theo dõi trên hệ thống. Nếu cần thay đổi, hãy liên hệ người phụ trách hoặc hotline, họ sẽ tư vấn và gọi lại cho bạn."
            : "Muốn thêm, bớt món, đổi số bàn hay dịch vụ, hãy báo người phụ trách. Họ sẽ cập nhật và ghi chú lại cho bạn."}
        </Alert>
        <List dense disablePadding>
          {contacts.map((c) => (
            <ListItem key={c.email} disableGutters>
              <ListItemAvatar><Avatar sx={{ bgcolor: c.role === "Primary" ? "primary.main" : "secondary.main" }}>{initials(c.fullName)}</Avatar></ListItemAvatar>
              <ListItemText
                primary={`${c.fullName} · ${c.role === "Primary" ? "Người phụ trách chính" : "Đồng phụ trách"}`}
                secondary={<>
                  {c.phone && <><Link href={`tel:${c.phone}`}>{c.phone}</Link>{" · "}</>}
                  <Link href={`mailto:${c.email}`}>{c.email}</Link>
                </>} />
            </ListItem>
          ))}
          {contacts.length === 0 && <Typography color="text.secondary">Chưa có người phụ trách.</Typography>}
        </List>
        {hotline && (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", mt: 1, p: 1.5, borderRadius: 1, bgcolor: "action.hover" }}>
            <PhoneIcon color="primary" />
            <Typography>Hotline tổng đài: <Link href={`tel:${hotline}`} sx={{ fontWeight: 700 }}>{hotline}</Link></Typography>
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
