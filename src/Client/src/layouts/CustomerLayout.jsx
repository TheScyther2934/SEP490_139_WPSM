import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Chip, Stack, Typography } from "@mui/material";
import DashboardIcon from "@mui/icons-material/Dashboard";
import DescriptionIcon from "@mui/icons-material/Description";
import PostAddIcon from "@mui/icons-material/PostAdd";
import TimelineIcon from "@mui/icons-material/Timeline";
import ChangeCircleIcon from "@mui/icons-material/ChangeCircle";
import PaymentsIcon from "@mui/icons-material/Payments";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import CelebrationIcon from "@mui/icons-material/Celebration";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import NotificationsIcon from "@mui/icons-material/Notifications";
import PortalShell from "./PortalShell";
import { PATHS } from "../routes/paths";
import { loadCustomerProject, selectCustomerState } from "../store/customerSlice";
import { dateVi } from "../utils/format";

const C = PATHS.customer;
const SECTIONS = [
  {
    items: [
      { label: "Tổng quan", icon: DashboardIcon, to: C.overview },
      { label: "Hợp đồng đặc tả", icon: DescriptionIcon, to: C.contract, end: true },
      { label: "Phụ lục hợp đồng", icon: PostAddIcon, to: C.addenda },
      { label: "Lộ trình chuẩn bị", icon: TimelineIcon, to: C.roadmap },
      { label: "Yêu cầu thay đổi", icon: ChangeCircleIcon, to: C.changeRequests },
      { label: "Thanh toán & dư nợ", icon: PaymentsIcon, to: C.payments },
      { label: "Ảnh thực tế hôn lễ", icon: PhotoLibraryIcon, to: C.livePhotos },
      { label: "Sau sự kiện", icon: CelebrationIcon, to: C.postEvent },
      { label: "Khiếu nại", icon: ReportProblemIcon, to: C.complaints },
      { label: "Thông báo", icon: NotificationsIcon, to: PATHS.notifications },
    ],
  },
];

// Loads the customer's own project once; customer pages read it from the store instead of fetching again.
export default function CustomerLayout() {
  const dispatch = useDispatch();
  const { status, snapshot } = useSelector(selectCustomerState);
  useEffect(() => { if (status === "idle") dispatch(loadCustomerProject()); }, [status, dispatch]);

  const project = snapshot?.project;
  const header = project ? (
    <Stack direction="row" spacing={1} sx={{ alignItems: "center", overflow: "hidden" }}>
      <Chip label={project.id} color="primary" size="small" />
      <Typography noWrap color="text.secondary">{project.hall} · {dateVi(project.eventDate)}</Typography>
    </Stack>
  ) : <Typography color="text.secondary">WPSM Portal</Typography>;

  return <PortalShell brand="WPSM Portal" sections={SECTIONS} header={header} />;
}
