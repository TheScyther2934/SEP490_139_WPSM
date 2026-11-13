import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import {
  AppBar, Box, Button, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, Menu, MenuItem, Toolbar, Tooltip, Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import NotificationsNoneIcon from "@mui/icons-material/NotificationsNone";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import { logout, selectUser } from "../store/authSlice";
import { PATHS } from "../routes/paths";
import { roleLabel } from "../utils/format";

// Frame with the navigation as one horizontal bar (mockups A-01..A-08); below `lg` the same items open in a drawer.
// `items` = [{ label, icon, to, end? }].
export default function TopNavShell({ brand, items, barColor }) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState(null);
  const user = useSelector(selectUser);
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const go = (to) => { setMenuAnchor(null); navigate(to); };
  const signOut = async () => {
    setMenuAnchor(null);
    await dispatch(logout());
    navigate(PATHS.login, { replace: true });
  };

  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "background.default" }}>
      <AppBar position="sticky" elevation={0} sx={{ bgcolor: barColor, color: "#fff" }}>
        <Toolbar variant="dense" sx={{ minHeight: 56, gap: 1, maxWidth: 1440, width: "100%", mx: "auto", px: { xs: 1.5, md: 3 } }}>
          <IconButton color="inherit" edge="start" aria-label="Mở menu" onClick={() => setDrawerOpen(true)} sx={{ display: { lg: "none" } }}>
            <MenuIcon />
          </IconButton>
          <Typography component={NavLink} to={items[0].to} sx={{ color: "inherit", textDecoration: "none", fontWeight: 800, letterSpacing: 0.5, mr: 2 }}>
            {brand}
          </Typography>

          <Box component="nav" aria-label="Điều hướng chính"
            sx={{ display: { xs: "none", lg: "flex" }, flex: 1, gap: 0.5, overflowX: "auto", scrollbarWidth: "none" }}>
            {items.map(({ label, to, end }) => (
              <Button key={to} component={NavLink} to={to} end={end} color="inherit" size="small"
                sx={{
                  px: 1.5, py: 0.75, whiteSpace: "nowrap", fontWeight: 500, color: "rgba(255,255,255,0.85)", borderRadius: 1,
                  "&:hover": { bgcolor: "rgba(255,255,255,0.12)" },
                  "&.active": { bgcolor: "#fff", color: barColor, fontWeight: 700 },
                }}>
                {label}
              </Button>
            ))}
          </Box>
          <Box sx={{ flex: { xs: 1, lg: "0 0 auto" } }} />

          <Tooltip title="Thông báo">
            <IconButton color="inherit" component={NavLink} to={PATHS.notifications} aria-label="Thông báo"><NotificationsNoneIcon /></IconButton>
          </Tooltip>
          <Button color="inherit" endIcon={<ArrowDropDownIcon />} onClick={(e) => setMenuAnchor(e.currentTarget)} aria-label="Tài khoản"
            sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>
            {roleLabel(user?.role)}
          </Button>
          <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)} anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
            transformOrigin={{ vertical: "top", horizontal: "right" }}>
            <Box sx={{ px: 2, py: 1, minWidth: 220 }}>
              <Typography fontWeight={600}>{user?.fullName}</Typography>
              <Typography variant="body2" color="text.secondary">{user?.email}</Typography>
            </Box>
            <Divider />
            <MenuItem onClick={() => go(PATHS.profile)}>Hồ sơ cá nhân</MenuItem>
            <MenuItem onClick={() => go(PATHS.notifications)}>Thông báo</MenuItem>
            <MenuItem onClick={signOut} sx={{ color: "error.main" }}>Đăng xuất</MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Drawer open={drawerOpen} onClose={() => setDrawerOpen(false)} sx={{ display: { lg: "none" }, "& .MuiDrawer-paper": { width: 264 } }}>
        <Toolbar sx={{ bgcolor: barColor, color: "#fff" }}><Typography fontWeight={800}>{brand}</Typography></Toolbar>
        <List component="nav" aria-label="Điều hướng chính">
          {items.map(({ label, icon: Icon, to, end }) => (
            <ListItemButton key={to} component={NavLink} to={to} end={end} onClick={() => setDrawerOpen(false)}
              sx={{ mx: 1, borderRadius: 1, "&.active": { bgcolor: barColor, color: "#fff", "& .MuiListItemIcon-root": { color: "inherit" } } }}>
              <ListItemIcon sx={{ minWidth: 40 }}><Icon fontSize="small" /></ListItemIcon>
              <ListItemText primary={label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ maxWidth: 1440, mx: "auto", px: { xs: 2, md: 4 }, py: { xs: 2, md: 4 } }}>
        <Outlet />
      </Box>
    </Box>
  );
}
