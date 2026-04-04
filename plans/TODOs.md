- refactor all UIs to optimize ImGui code
    - pre-compute data whenever possible and cache in instance or static variables as appropriate
    - minimize re-calculating values unnecessarily
    - ensure imgui code doesnt run when the window, child window or collapsed headers etc are not open/visible

