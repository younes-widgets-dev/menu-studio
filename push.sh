export GH_TOKEN=$(cat /tmp/ghtok.txt)
cd /tmp/ms && git add -A && git -c user.name=younes-widgets-dev -c user.email=younes00@mail.instinct.com commit -qm "$1" && git push -q https://younes-widgets-dev:${GH_TOKEN}@github.com/younes-widgets-dev/menu-studio.git HEAD:main 2>&1 | tail -2
