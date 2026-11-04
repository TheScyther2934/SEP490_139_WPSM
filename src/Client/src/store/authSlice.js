import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { authService } from "../services/authService";
import { errorMessage } from "../utils/format";

// status: "unknown" until the first refresh answers, then "authenticated" or "anonymous".
// The access token lives only in memory; the refresh token is an HttpOnly cookie the browser keeps.
const initialState = { status: "unknown", restoring: false, accessToken: null, expiresAt: null, user: null };

const applyTokens = (state, body) => {
  state.status = "authenticated";
  state.accessToken = body.accessToken;
  state.expiresAt = body.expiresAt;
  state.user = body.user;
};

// Called once on start-up (F5 loses the in-memory token) and by axiosClient after a 401.
// The condition stops StrictMode or parallel callers from rotating the refresh token twice.
export const refreshSession = createAsyncThunk("auth/refresh", () => authService.refresh(), {
  condition: (_, { getState }) => !getState().auth.restoring,
});

export const login = createAsyncThunk("auth/login", async ({ email, password }, { rejectWithValue }) => {
  try {
    return await authService.login(email, password);
  } catch (error) {
    return rejectWithValue(errorMessage(error));
  }
});

export const logout = createAsyncThunk("auth/logout", async () => {
  try {
    await authService.revoke();
  } catch {
    // The local session ends even when the server could not be reached.
  }
});

// With a Bearer token the API answers a fresh token pair and revokes every other session.
export const changePassword = createAsyncThunk("auth/changePassword", async (body, { rejectWithValue }) => {
  try {
    return await authService.changePassword(body);
  } catch (error) {
    return rejectWithValue(errorMessage(error));
  }
});

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    sessionExpired: () => ({ ...initialState, status: "anonymous" }),
  },
  extraReducers: (builder) => {
    builder
      .addCase(refreshSession.pending, (state) => { state.restoring = true; })
      .addCase(refreshSession.fulfilled, (state, { payload }) => { state.restoring = false; applyTokens(state, payload); })
      .addCase(refreshSession.rejected, () => ({ ...initialState, status: "anonymous" }))
      .addCase(login.fulfilled, (state, { payload }) => applyTokens(state, payload))
      .addCase(changePassword.fulfilled, (state, { payload }) => {
        if (payload?.accessToken) applyTokens(state, payload);
        else if (state.user) state.user.mustChangePassword = false;
      })
      .addCase(logout.fulfilled, () => ({ ...initialState, status: "anonymous" }));
  },
});

export const { sessionExpired } = authSlice.actions;
export const selectAuth = (state) => state.auth;
export const selectUser = (state) => state.auth.user;
export default authSlice.reducer;
