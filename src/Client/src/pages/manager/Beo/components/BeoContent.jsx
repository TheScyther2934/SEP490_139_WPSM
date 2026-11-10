import { useState } from "react";
import { Card, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs } from "@mui/material";
import { DEPARTMENTS } from "../departments";
import { dateTimeVi } from "../../../../utils/format";
import StatusChip from "../../../../components/StatusChip";

// Content tabs compiled from the baseline and approved addenda (SDS IV.4.2); read-only.
export default function BeoContent({ beo }) {
  const [tab, setTab] = useState("timeline");
  return (
    <Card>
      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" sx={{ borderBottom: 1, borderColor: "divider" }}>
        <Tab value="timeline" label="Lịch trình" />
        <Tab value="menu" label="Thực đơn và dịch vụ" />
        <Tab value="departments" label="Theo bộ phận" />
        <Tab value="versions" label="Lịch sử phiên bản" />
      </Tabs>
      <Table size="small" id={`beo-${tab}`}>
        {tab === "timeline" && (
          <>
            <TableHead><TableRow><TableCell>Giờ</TableCell><TableCell>Công việc</TableCell><TableCell>Bộ phận</TableCell></TableRow></TableHead>
            <TableBody>
              {beo.timeline.map((t, i) => (
                <TableRow key={i}><TableCell>{t.time}</TableCell><TableCell>{t.activity}</TableCell><TableCell>{DEPARTMENTS[t.department]}</TableCell></TableRow>
              ))}
            </TableBody>
          </>
        )}
        {tab === "menu" && (
          <>
            <TableHead><TableRow><TableCell>Nhóm</TableCell><TableCell>Hạng mục</TableCell><TableCell>Mô tả</TableCell><TableCell align="right">Số lượng</TableCell></TableRow></TableHead>
            <TableBody>
              {beo.menu.map((l, i) => (
                <TableRow key={i}><TableCell>{l.category}</TableCell><TableCell>{l.item}</TableCell><TableCell>{l.description}</TableCell>
                  <TableCell align="right">{l.quantity} {l.unit}</TableCell></TableRow>
              ))}
            </TableBody>
          </>
        )}
        {tab === "departments" && (
          <>
            <TableHead><TableRow><TableCell>Bộ phận</TableCell><TableCell>Hạng mục</TableCell><TableCell align="right">Số lượng</TableCell></TableRow></TableHead>
            <TableBody>
              {beo.departments.flatMap((d) => d.lines.map((l, i) => (
                <TableRow key={`${d.department}-${i}`}><TableCell>{i === 0 ? DEPARTMENTS[d.department] : ""}</TableCell><TableCell>{l.item} – {l.description}</TableCell>
                  <TableCell align="right">{l.quantity} {l.unit}</TableCell></TableRow>
              )))}
            </TableBody>
          </>
        )}
        {tab === "versions" && (
          <>
            <TableHead><TableRow><TableCell>Phiên bản</TableCell><TableCell>Trạng thái</TableCell><TableCell>Đóng băng lúc</TableCell><TableCell>Người duyệt</TableCell></TableRow></TableHead>
            <TableBody>
              {beo.versions.length === 0 ? <TableRow><TableCell colSpan={4}>Chưa có phiên bản BEO.</TableCell></TableRow> : beo.versions.map((v) => (
                <TableRow key={v.version}><TableCell>v{v.version}</TableCell><TableCell><StatusChip kind="beo" status={v.status} /></TableCell>
                  <TableCell>{dateTimeVi(v.frozenAt)}</TableCell><TableCell>{v.approvedBy ?? "—"}</TableCell></TableRow>
              ))}
            </TableBody>
          </>
        )}
      </Table>
    </Card>
  );
}
