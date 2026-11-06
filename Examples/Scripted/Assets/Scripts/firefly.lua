-- A firefly: drifts, bounces off the screen edges and blinks by switching self.color.
-- The game sets the position. Velocity and the blink clock are the script's own state in self.data.
local Firefly = {}

local BLINK_SECONDS = 0.6

math.randomseed(1)

function Firefly.on_start(self)
    self.data.vx = math.random() * 16 - 8
    self.data.vy = math.random() * 8 - 4
    self.data.time = math.random() * BLINK_SECONDS -- start the blinking out of step
end

function Firefly.on_update(self, dt)
    local data = self.data
    data.time = data.time + dt

    local x = self.x + data.vx * dt
    local y = self.y + data.vy * dt
    if x < 0 or x >= console.width then data.vx = -data.vx else self.x = x end
    if y < 1 or y >= console.height - 1 then data.vy = -data.vy else self.y = y end

    local lit = data.time % BLINK_SECONDS < BLINK_SECONDS / 2
    self.color = lit and colors.Green or colors.DarkGreen
end

return Firefly
