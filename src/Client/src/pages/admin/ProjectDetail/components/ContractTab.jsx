import { Box, Card, CardContent, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import StageNotes from "../../../manager/ProjectHub/components/StageNotes";
import { dateTimeVi, money } from "../../../../utils/format";

const Empty = ({ cols, text }) => <TableRow><TableCell colSpan={cols} align="center" sx={{ py: 3, color: "text.secondary" }}>{text}</TableCell></TableRow>;
const EVIDENCE = { Transfer: "Chuyển khoản", Invoice: "Hóa đơn", Contract: "Hợp đồng", Other: "Khác" };

// Contract content: services in force, stage notes (with adjustments), addon contracts, supplier evidence and complaints.
export default function ContractTab({ project }) {
  const total = project.specification.reduce((sum, x) => sum + x.lineTotal, 0);
  return (
    <Box sx={{ display: "grid", gap: 2 }}>
      <Section title="Dịch vụ đã đặt" subtitle={`Tổng ${money(total)} · giá trị lúc ký ${money(project.money.signedValue)}`}>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>Dịch vụ</TableCell><TableCell>Mô tả</TableCell><TableCell align="right">SL</TableCell>
            <TableCell align="right">Đơn giá</TableCell><TableCell align="right">Thành tiền</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {project.specification.length === 0 ? <Empty cols={5} text="Chưa có dịch vụ." /> : project.specification.map((x, i) => (
              <TableRow key={i}>
                <TableCell><Typography variant="body2" fontWeight={600}>{x.name}</Typography><Typography variant="caption" color="text.secondary">{x.category}</Typography></TableCell>
                <TableCell>{x.description}</TableCell>
                <TableCell align="right">{x.quantity} {x.unit}</TableCell>
                <TableCell align="right">{money(x.unitPrice)}</TableCell>
                <TableCell align="right">{money(x.lineTotal)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Section>

      <StageNotes code={project.projectCode} notes={project.notes} showForm={false} />

      <Section title="Hợp đồng phát sinh">
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>Mã</TableCell><TableCell>Nội dung</TableCell><TableCell>Người tạo</TableCell>
            <TableCell align="center">Trạng thái</TableCell><TableCell align="right">Giá trị</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {project.addons.length === 0 ? <Empty cols={5} text="Không có hợp đồng phát sinh." /> : project.addons.map((a) => (
              <TableRow key={a.code}>
                <TableCell><Typography variant="body2" fontWeight={600}>{a.code}</Typography><Typography variant="caption" color="text.secondary">{dateTimeVi(a.createdAt)}</Typography></TableCell>
                <TableCell><Typography variant="body2">{a.title}</Typography>
                  <Typography variant="caption" color="text.secondary">{a.items.map((i) => `${i.description} × ${i.quantity}`).join(", ")}</Typography></TableCell>
                <TableCell>{a.createdBy}{a.confirmedAt && <Typography variant="caption" color="text.secondary" component="div">Khách xác nhận {dateTimeVi(a.confirmedAt)}</Typography>}</TableCell>
                <TableCell align="center"><StatusChip kind="addon" status={a.status} /></TableCell>
                <TableCell align="right">{money(a.total)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Section>

      <Section title="Minh chứng nhà cung cấp">
        <Table size="small">
          <TableHead><TableRow><TableCell>Nhà cung cấp</TableCell><TableCell>Dịch vụ</TableCell><TableCell>Loại</TableCell><TableCell>Người tải · lúc</TableCell></TableRow></TableHead>
          <TableBody>
            {project.suppliers.length === 0 ? <Empty cols={4} text="Chưa có minh chứng." /> : project.suppliers.map((e) => (
              <TableRow key={e.id}>
                <TableCell><Typography variant="body2" fontWeight={600}>{e.supplierName}</Typography><Typography variant="caption" color="text.secondary">{e.fileName}</Typography></TableCell>
                <TableCell>{e.serviceName ?? "—"}</TableCell>
                <TableCell><Chip size="small" label={EVIDENCE[e.evidenceType] ?? e.evidenceType} /></TableCell>
                <TableCell>{e.uploadedBy} · {dateTimeVi(e.uploadedAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Section>

      <Section title="Khiếu nại">
        <Table size="small">
          <TableHead><TableRow><TableCell>Mã</TableCell><TableCell>Nội dung</TableCell><TableCell align="center">Trạng thái</TableCell><TableCell>Kết quả xử lý</TableCell></TableRow></TableHead>
          <TableBody>
            {project.complaints.length === 0 ? <Empty cols={4} text="Không có khiếu nại." /> : project.complaints.map((c) => (
              <TableRow key={c.code}>
                <TableCell><Typography variant="body2" fontWeight={600}>{c.code}</Typography><Typography variant="caption" color="text.secondary">{dateTimeVi(c.createdAt)}</Typography></TableCell>
                <TableCell>{c.description}</TableCell>
                <TableCell align="center"><StatusChip kind="complaint" status={c.status} /></TableCell>
                <TableCell>{c.resolutionNote ?? "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Section>
    </Box>
  );
}

function Section({ title, subtitle, children }) {
  return (
    <Card>
      <CardContent sx={{ pb: 1 }}>
        <Typography variant="h6">{title}</Typography>
        {subtitle && <Typography variant="body2" color="text.secondary">{subtitle}</Typography>}
      </CardContent>
      <TableContainer>{children}</TableContainer>
    </Card>
  );
}
