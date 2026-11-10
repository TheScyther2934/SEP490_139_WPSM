import { Card, Chip, List, ListItemButton, ListItemText, Stack } from "@mui/material";
import { NOTIFICATION_TYPES } from "../notificationLinks";
import { dateTimeVi } from "../../../../utils/format";

// Newest first; unread rows are bold; clicking marks read and opens the related screen.
export default function NotificationList({ items, onOpen }) {
  return (
    <Card>
      <List id="notificationList" disablePadding>
        {items.map((n) => {
          const type = NOTIFICATION_TYPES[n.type] ?? { label: n.type, color: "default" };
          return (
            <ListItemButton key={n.id} divider onClick={() => onOpen(n)} data-notification={n.id} data-read={n.isRead}>
              <ListItemText
                primary={<Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <Chip size="small" label={type.label} color={type.color} />
                  <span style={{ fontWeight: n.isRead ? 400 : 700 }}>{n.message}</span>
                </Stack>}
                secondary={dateTimeVi(n.createdAt)} />
            </ListItemButton>
          );
        })}
      </List>
    </Card>
  );
}
