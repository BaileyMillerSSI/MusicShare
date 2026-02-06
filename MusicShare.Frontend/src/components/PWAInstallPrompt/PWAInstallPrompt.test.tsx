import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

// Mock the hook before importing the component so the module graph is wired
// correctly. Each test case controls what the hook returns.
vi.mock("../../hooks/usePWAInstall", () => ({
    usePWAInstall: vi.fn(),
}));

import { usePWAInstall } from "../../hooks/usePWAInstall";
import { PWAInstallPrompt } from "./PWAInstallPrompt";

// Helper that returns a full UsePWAInstallReturn shape with safe defaults.
function mockHookReturn(
    overrides: Partial<ReturnType<typeof usePWAInstall>> = {},
) {
    return {
        canInstall: false,
        isIOSDevice: false,
        isInstalled: false,
        promptInstall: vi.fn(),
        dismissPrompt: vi.fn(),
        ...overrides,
    };
}

describe("PWAInstallPrompt", () => {
    it("renders nothing when canInstall is false", () => {
        vi.mocked(usePWAInstall).mockReturnValue(
            mockHookReturn({ canInstall: false }),
        );

        const { container } = render(<PWAInstallPrompt />);

        expect(container.innerHTML).toBe("");
    });

    it("renders InstallBanner on a non-iOS device when canInstall is true", () => {
        vi.mocked(usePWAInstall).mockReturnValue(
            mockHookReturn({ canInstall: true, isIOSDevice: false }),
        );

        render(<PWAInstallPrompt />);

        // InstallBanner renders these elements; IOSInstallInstructions does not.
        expect(screen.getByRole("button", { name: "Install" })).toBeVisible();
        expect(screen.getByRole("button", { name: "Not now" })).toBeVisible();
        expect(screen.getByText("Install Music Share")).toBeVisible();
    });

    it("renders IOSInstallInstructions on an iOS device when canInstall is true", () => {
        vi.mocked(usePWAInstall).mockReturnValue(
            mockHookReturn({ canInstall: true, isIOSDevice: true }),
        );

        render(<PWAInstallPrompt />);

        // IOSInstallInstructions renders these; InstallBanner does not.
        expect(
            screen.getByRole("heading", { name: "Install Music Share" }),
        ).toBeVisible();
        expect(screen.getByRole("button", { name: "Got it" })).toBeVisible();
        expect(
            screen.queryByRole("button", { name: "Install" }),
        ).not.toBeInTheDocument();
    });

    it("does not render IOSInstallInstructions on a non-iOS device", () => {
        vi.mocked(usePWAInstall).mockReturnValue(
            mockHookReturn({ canInstall: true, isIOSDevice: false }),
        );

        render(<PWAInstallPrompt />);

        expect(
            screen.queryByRole("button", { name: "Got it" }),
        ).not.toBeInTheDocument();
    });

    it("does not render InstallBanner on an iOS device", () => {
        vi.mocked(usePWAInstall).mockReturnValue(
            mockHookReturn({ canInstall: true, isIOSDevice: true }),
        );

        render(<PWAInstallPrompt />);

        expect(
            screen.queryByRole("button", { name: "Not now" }),
        ).not.toBeInTheDocument();
    });
});
