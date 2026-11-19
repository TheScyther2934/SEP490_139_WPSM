import { Typography } from "@mui/material";
import DashboardIcon from "@mui/icons-material/Dashboard";
import FolderSpecialIcon from "@mui/icons-material/FolderSpecial";
import AddCircleIcon from "@mui/icons-material/AddCircle";
import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import WarningIcon from "@mui/icons-material/Warning";
import NotificationsIcon from "@mui/icons-material/Notifications";
import PortalShell from "./PortalShell";
import { PATHS } from "../routes/paths";

const M = PATHS.manager;
const SECTIONS = [
  {
    title: "Điều hành vận hành",
    items: [
      { label: "Tổng quan", icon: DashboardIcon, to: M.dashboard },
      // `end` is false so the item stays active inside a project; exclude `M.newProject` so both aren't highlighted together.
      { label: "Sự kiện", icon: FolderSpecialIcon, to: M.projects, exclude: M.newProject },
      { label: "Tạo hợp đồng", icon: AddCircleIcon, to: M.newProject },
      { label: "Lịch thiết bị", icon: CalendarMonthIcon, to: M.resourceCalendar },
      { label: "Việc trễ hạn", icon: WarningIcon, to: M.overdueAlerts },
      { label: "Thông báo", icon: NotificationsIcon, to: PATHS.notifications },
    ],
  },
];

export default function ManagerLayout() {
  return <PortalShell brand="WPSM Manager" sections={SECTIONS} header={<Typography color="text.secondary">Quản lý vận hành</Typography>} />;
}
