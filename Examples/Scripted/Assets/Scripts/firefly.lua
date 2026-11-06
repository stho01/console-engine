-- A firefly: picks a random velocity, bounces off the screen edges and blinks by switching self.color.
-- The engine's MovementSystem moves it by self.vx, self.vy. The blink clock is the script's own state in self.data.
local Firefly = {}

local BLINK_SECONDS = 0.6

math.randomseed(1)

function Firefly.on_start(self)
    self.vx = math.random() * 16 - 8
    self.vy = math.random() * 8 - 4
    self.data.time = math.random() * BLINK_SECONDS -- start the blinking out of step
end

function Firefly.on_update(self, dt)
    local data = self.data
    data.time = data.time + dt

    if self.x < 0 and self.vx < 0 or self.x >= console.width - 1 and self.vx > 0 then self.vx = -self.vx end
    if self.y < 1 and self.vy < 0 or self.y >= console.height - 2 and self.vy > 0 then self.vy = -self.vy end

    local lit = data.time % BLINK_SECONDS < BLINK_SECONDS / 2
    self.color = lit and colors.Green or colors.DarkGreen
end

return Firefly
