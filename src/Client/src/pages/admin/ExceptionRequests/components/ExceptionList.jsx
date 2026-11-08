import { Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import { dateTimeVi } from "../../../../utils/format";
import { impactLabel, sourceColor, sourceLabel, waitingLabel } from "../exceptionMeta";

const clampTwo = { display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden" };

// Left-hand queue of A-18. Pending requests show how long they have been waiting; decided ones show when.
export default function ExceptionList({ requests, selectedId, onOpen }) {
  return (
    <Stack spacing={1.5}>
      {requests.map((request) => (
        <Card key={request.id} id={`a18Card-${request.id}`} onClick={() => onOpen(request)}
          sx={{
            cursor: "pointer", borderColor: request.id === selectedId ? "primary.main" : "divider",
            borderWidth: request.id === selectedId ? 2 : 1,
            bgcolor: request.id === selectedId ? "rgba(109, 22, 52, 0.03)" : "background.paper",
          }}>
          <CardContent sx={{ pb: "12px !important" }}>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1, gap: 1 }}>
              <Chip size="small" color={sourceColor(request.sourceType)} label={sourceLabel(request.sourceType)} />
              <Typography variant="caption" color={request.status === "Pending" ? "warning.main" : "text.secondary"} fontWeight={request.status === "Pending" ? 600 : 400}>
                {request.status === "Pending" ? waitingLabel(request.createdAt) : dateTimeVi(request.decidedAt ?? request.createdAt)}
              </Typography>
            </Stack>
            <Typography variant="subtitle2" color="primary">{request.code} · {request.projectCode}</Typography>
            <Typography variant="body2" fontWeight={600} noWrap title={request.projectCouple}>{request.projectCouple}</Typography>
            <Typography variant="caption" color="text.secondary" component="p">
              {request.requestedByName} · {request.hallName}
            </Typography>
            <Typography variant="body2" sx={{ ...clampTwo, mt: 1, minHeight: 42 }}>{request.note}</Typography>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mt: 1, gap: 1 }}>
              <Typography variant="caption" fontWeight={600} color={request.cr ? "secondary.main" : "error.main"}>{impactLabel(request)}</Typography>
              <Typography variant="caption" color="primary" fontWeight={600}>Xem chi tiết →</Typography>
            </Stack>
          </CardContent>
        </Card>
      ))}
    </Stack>
  );
}
