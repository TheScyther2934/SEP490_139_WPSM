import { useState, useEffect, useCallback, useTransition } from "react";
import { useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import {
  Alert,
  Badge,
  Box,
  Button,
  Card,
  CardActionArea,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Pagination,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  Tooltip,
  Typography,
} from "@mui/material";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import MarkEmailReadIcon from "@mui/icons-material/MarkEmailRead";
import NotificationsNoneIcon from "@mui/icons-material/NotificationsNone";
import TimelineIcon from "@mui/icons-material/Timeline";
import PaymentsIcon from "@mui/icons-material/Payments";
import ChangeCircleIcon from "@mui/icons-material/ChangeCircle";
import DescriptionIcon from "@mui/icons-material/Description";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import RefreshIcon from "@mui/icons-material/Refresh";

import PageHeader from "../../../components/PageHeader";
import { notificationService } from "../../../services/notificationService";
import { selectUser } from "../../../store/authSlice";
import { notify } from "../../../store/notifySlice";
import { dateTimeVi, relativeTimeVi, errorMessage } from "../../../utils/format";
import { resolveNotificationRoute } from "../../../utils/notificationRouting";

const PAGE_SIZE = 15;

const TYPE_CONFIG = {
  "Roadmap reminder": { label: "Lộ trình", color: "info", icon: TimelineIcon },
  Payment: { label: "Thanh toán", color: "success", icon: PaymentsIcon },
  "CR update": { label: "Yêu cầu thay đổi", color: "warning", icon: ChangeCircleIcon },
  BEO: { label: "BEO sự kiện", color: "secondary", icon: DescriptionIcon },
  "Overdue alert": { label: "Quá hạn", color: "error", icon: WarningAmberIcon },
  Complaint: { label: "Khiếu nại", color: "warning", icon: ReportProblemIcon },
};

export default function Notifications() {
  const user = useSelector(selectUser);
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const [, startTransition] = useTransition();

  const [activeTab, setActiveTab] = useState("All"); // "All" | "Unread"
  const [page, setPage] = useState(1);
  const [data, setData] = useState({ items: [], totalCount: 0, unreadCount: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [markingAll, setMarkingAll] = useState(false);
  const [readingId, setReadingId] = useState(null);

  const fetchNotifications = useCallback(
    async (filter = activeTab, targetPage = page) => {
      setLoading(true);
      setError(null);
      try {
        const res = await notificationService.getNotifications(filter, targetPage, PAGE_SIZE);
        setData(res);
      } catch (err) {
        setError(errorMessage(err));
      } finally {
        setLoading(false);
      }
    },
    [activeTab, page]
  );

  useEffect(() => {
    fetchNotifications(activeTab, page);
  }, [fetchNotifications, activeTab, page]);

  const handleTabChange = (_e, nextTab) => {
    if (nextTab === activeTab) return;
    setActiveTab(nextTab);
    setPage(1);
  };

  const handleMarkAsRead = async (e, notification) => {
    e.stopPropagation();
    if (notification.isRead || readingId === notification.notificationId) return;

    setReadingId(notification.notificationId);
    try {
      await notificationService.markAsRead(notification.notificationId);
      setData((prev) => ({
        ...prev,
        unreadCount: Math.max(0, prev.unreadCount - 1),
        items: prev.items.map((item) =>
          item.notificationId === notification.notificationId
            ? { ...item, isRead: true }
            : item
        ),
      }));
      dispatch(notify({ message: "Đã đánh dấu thông báo là đã đọc.", severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setReadingId(null);
    }
  };

  const handleMarkAllAsRead = async () => {
    if (markingAll || data.unreadCount === 0) return;
    setMarkingAll(true);
    try {
      const result = await notificationService.markAllAsRead();
      setData((prev) => ({
        ...prev,
        unreadCount: 0,
        items: prev.items.map((item) => ({ ...item, isRead: true })),
      }));
      dispatch(
        notify({
          message: `Đã đánh dấu tất cả ${result.updatedCount ?? ""} thông báo là đã đọc.`,
          severity: "success",
        })
      );
      if (activeTab === "Unread") {
        setPage(1);
      }
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setMarkingAll(false);
    }
  };

  const handleNotificationClick = async (notification) => {
    // 1. Mark as read immediately if unread
    if (!notification.isRead) {
      try {
        notificationService.markAsRead(notification.notificationId);
        setData((prev) => ({
          ...prev,
          unreadCount: Math.max(0, prev.unreadCount - 1),
          items: prev.items.map((item) =>
            item.notificationId === notification.notificationId
              ? { ...item, isRead: true }
              : item
          ),
        }));
      } catch (e) {
        // Non-blocking for navigation
      }
    }

    // 2. Safe route resolution based on role & target
    const targetRoute = resolveNotificationRoute(notification, user?.role);
    if (targetRoute) {
      startTransition(() => {
        navigate(targetRoute);
      });
    } else {
      dispatch(
        notify({
          message: "Thông báo này không có màn hình liên kết hoặc bạn không có quyền truy cập.",
          severity: "info",
        })
      );
    }
  };

  const totalPages = Math.ceil((data.totalCount || 0) / PAGE_SIZE);

  return (
    <Box sx={{ maxWidth: 960, mx: "auto", pb: 4 }}>
      <PageHeader
        code="G-04"
        title="Thông báo"
        subtitle="Theo dõi tiến độ, thanh toán, thay đổi và các cập nhật vận hành quan trọng"
        actions={
          <Stack direction="row" spacing={1}>
            <Tooltip title="Tải lại danh sách">
              <span>
                <IconButton
                  aria-label="Tải lại"
                  onClick={() => fetchNotifications(activeTab, page)}
                  disabled={loading}
                  size="small"
                >
                  <RefreshIcon />
                </IconButton>
              </span>
            </Tooltip>
            <Button
              variant="outlined"
              color="primary"
              startIcon={markingAll ? <CircularProgress size={16} /> : <DoneAllIcon />}
              onClick={handleMarkAllAsRead}
              disabled={markingAll || data.unreadCount === 0 || loading}
            >
              Đánh dấu tất cả đã đọc
            </Button>
          </Stack>
        }
      />

      {/* Tabs Filter */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 3 }}>
        <Tabs
          value={activeTab}
          onChange={handleTabChange}
          textColor="primary"
          indicatorColor="primary"
        >
          <Tab
            value="All"
            label={
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <span>Tất cả</span>
                {data.totalCount > 0 && activeTab === "All" && (
                  <Chip label={data.totalCount} size="small" variant="outlined" />
                )}
              </Stack>
            }
          />
          <Tab
            value="Unread"
            label={
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <span>Chưa đọc</span>
                {data.unreadCount > 0 && (
                  <Badge
                    badgeContent={data.unreadCount}
                    color="error"
                    sx={{ ml: 1, "& .MuiBadge-badge": { position: "static", transform: "none" } }}
                  />
                )}
              </Stack>
            }
          />
        </Tabs>
      </Box>

      {/* Error State */}
      {error && (
        <Alert
          severity="error"
          sx={{ mb: 3 }}
          action={
            <Button color="inherit" size="small" onClick={() => fetchNotifications(activeTab, page)}>
              Thử lại
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {/* Loading Skeletons */}
      {loading && !error && (
        <Stack spacing={2}>
          {[1, 2, 3, 4].map((i) => (
            <Card key={i} variant="outlined" sx={{ p: 2 }}>
              <Stack direction="row" spacing={2} sx={{ alignItems: "flex-start" }}>
                <Skeleton variant="circular" width={40} height={40} />
                <Box sx={{ flex: 1 }}>
                  <Skeleton variant="text" width="30%" height={24} />
                  <Skeleton variant="text" width="90%" height={20} />
                  <Skeleton variant="text" width="40%" height={16} />
                </Box>
              </Stack>
            </Card>
          ))}
        </Stack>
      )}

      {/* Empty State */}
      {!loading && !error && data.items.length === 0 && (
        <Card variant="outlined" sx={{ py: 8, px: 2, textAlign: "center", bgcolor: "background.paper" }}>
          <NotificationsNoneIcon sx={{ fontSize: 64, color: "text.disabled", mb: 2 }} />
          <Typography variant="h6" color="text.secondary" gutterBottom>
            {activeTab === "Unread"
              ? "Bạn không có thông báo chưa đọc nào"
              : "Chưa có thông báo nào"}
          </Typography>
          <Typography variant="body2" color="text.disabled">
            {activeTab === "Unread"
              ? "Tất cả các thông báo của bạn đã được đánh dấu là đã đọc."
              : "Các thông báo về tiến độ dự án, thanh toán và sự kiện sẽ hiển thị tại đây."}
          </Typography>
        </Card>
      )}

      {/* Notification List */}
      {!loading && !error && data.items.length > 0 && (
        <Stack spacing={1.5}>
          {data.items.map((n) => {
            const config = TYPE_CONFIG[n.type] ?? {
              label: n.type || "Thông báo",
              color: "default",
              icon: NotificationsNoneIcon,
            };
            const Icon = config.icon;

            return (
              <Card
                key={n.notificationId}
                variant="outlined"
                sx={{
                  position: "relative",
                  transition: "all 0.2s ease-in-out",
                  borderLeft: (theme) =>
                    !n.isRead
                      ? `5px solid ${theme.palette.primary.main}`
                      : `1px solid ${theme.palette.divider}`,
                  bgcolor: (theme) =>
                    !n.isRead
                      ? theme.palette.mode === "dark"
                        ? "rgba(25, 118, 210, 0.08)"
                        : "rgba(25, 118, 210, 0.03)"
                      : "background.paper",
                  "&:hover": {
                    boxShadow: 2,
                    borderColor: "primary.light",
                  },
                }}
              >
                <CardActionArea
                  onClick={() => handleNotificationClick(n)}
                  sx={{ p: 2, alignItems: "stretch" }}
                >
                  <Stack direction="row" spacing={2} sx={{ alignItems: "flex-start" }}>
                    {/* Type Icon Badge */}
                    <Box
                      sx={{
                        width: 42,
                        height: 42,
                        borderRadius: "50%",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        bgcolor: (theme) =>
                          n.isRead
                            ? theme.palette.action.selected
                            : `${theme.palette[config.color]?.main || theme.palette.primary.main}15`,
                        color: (theme) =>
                          n.isRead
                            ? theme.palette.text.secondary
                            : theme.palette[config.color]?.main || theme.palette.primary.main,
                        flexShrink: 0,
                      }}
                    >
                      <Icon fontSize="small" />
                    </Box>

                    {/* Content */}
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Stack
                        direction="row"
                        spacing={1}
                        sx={{
                          alignItems: "center",
                          justifyContent: "space-between",
                          mb: 0.5,
                          flexWrap: "wrap",
                          gap: 0.5,
                        }}
                      >
                        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                          <Chip
                            label={config.label}
                            color={config.color}
                            size="small"
                            variant={n.isRead ? "outlined" : "filled"}
                            sx={{ fontWeight: 500, fontSize: "0.75rem" }}
                          />
                          {!n.isRead && (
                            <Chip
                              label="Mới"
                              color="error"
                              size="small"
                              sx={{ height: 20, fontSize: "0.6875rem", fontWeight: 700 }}
                            />
                          )}
                          <Typography variant="caption" color="text.disabled">
                            #{n.notificationId}
                          </Typography>
                        </Stack>

                        <Typography
                          variant="caption"
                          color="text.secondary"
                          title={dateTimeVi(n.createdAt)}
                          sx={{ whiteSpace: "nowrap" }}
                        >
                          {relativeTimeVi(n.createdAt)}
                        </Typography>
                      </Stack>

                      {/* Message */}
                      <Typography
                        variant="body1"
                        sx={{
                          fontWeight: n.isRead ? 400 : 600,
                          color: n.isRead ? "text.primary" : "primary.dark",
                          lineHeight: 1.5,
                          wordBreak: "break-word",
                          mb: 0.75,
                        }}
                      >
                        {n.message}
                      </Typography>

                      {/* Footer Info */}
                      <Stack
                        direction="row"
                        spacing={2}
                        sx={{
                          alignItems: "center",
                          justifyContent: "space-between",
                          pt: 0.5,
                        }}
                      >
                        <Typography variant="caption" color="text.disabled">
                          {dateTimeVi(n.createdAt)}
                        </Typography>

                        {!n.isRead && (
                          <Tooltip title="Đánh dấu đã đọc">
                            <span>
                              <IconButton
                                size="small"
                                onClick={(e) => handleMarkAsRead(e, n)}
                                disabled={readingId === n.notificationId}
                                sx={{ color: "text.secondary", "&:hover": { color: "primary.main" } }}
                              >
                                {readingId === n.notificationId ? (
                                  <CircularProgress size={16} />
                                ) : (
                                  <MarkEmailReadIcon fontSize="small" />
                                )}
                              </IconButton>
                            </span>
                          </Tooltip>
                        )}
                      </Stack>
                    </Box>
                  </Stack>
                </CardActionArea>
              </Card>
            );
          })}
        </Stack>
      )}

      {/* Pagination */}
      {!loading && totalPages > 1 && (
        <Box sx={{ display: "flex", justifyContent: "center", mt: 4 }}>
          <Pagination
            count={totalPages}
            page={page}
            onChange={(_e, val) => setPage(val)}
            color="primary"
            shape="rounded"
          />
        </Box>
      )}
    </Box>
  );
}
