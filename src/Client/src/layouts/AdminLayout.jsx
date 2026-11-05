import { Typography } from "@mui/material";
import DashboardIcon from "@mui/icons-material/Dashboard";
import CategoryIcon from "@mui/icons-material/Category";
import PriceChangeIcon from "@mui/icons-material/PriceChange";
import GavelIcon from "@mui/icons-material/Gavel";
import AssessmentIcon from "@mui/icons-material/Assessment";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import WarningIcon from "@mui/icons-material/Warning";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import RuleIcon from "@mui/icons-material/Rule";
import PortalShell from "./PortalShell";
import { PATHS } from "../routes/paths";

const A = PATHS.admin;
const SECTIONS = [
  { items: [{ label: "Tổng quan", icon: DashboardIcon, to: A.dashboard }] },
  {
    title: "Cấu hình",
    items: [
      { label: "Danh mục dịch vụ", icon: CategoryIcon, to: A.catalog },
      { label: "Khung giá", icon: PriceChangeIcon, to: A.priceFrames },
      { label: "Chính sách", icon: GavelIcon, to: A.policies },
    ],
  },
  {
    title: "Giám sát",
    items: [
      { label: "Báo cáo", icon: AssessmentIcon, to: A.reports },
      { label: "Ảnh minh chứng", icon: PhotoLibraryIcon, to: A.photoFeed },
      { label: "Cảnh báo quá hạn", icon: WarningIcon, to: A.overdueAlerts },
      { label: "Khiếu nại chuyển cấp", icon: ReportProblemIcon, to: A.complaints },
      { label: "Yêu cầu ngoại lệ", icon: RuleIcon, to: A.exceptionRequests },
    ],
  },
];

export default function AdminLayout() {
  return <PortalShell brand="WPSM Executive" sections={SECTIONS} header={<Typography color="text.secondary">Hệ thống điều hành</Typography>} />;
}
