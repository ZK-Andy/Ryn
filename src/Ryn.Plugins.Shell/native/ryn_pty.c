#if !defined(_WIN32)

#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <sys/wait.h>

#if defined(__APPLE__)
#include <util.h>
#else
#include <pty.h>
#endif

/*
 * Spawns a child process with a PTY entirely in native code.
 * This avoids the dangerous pattern of doing managed work after fork()
 * in a .NET runtime, where the forked process inherits a potentially
 * inconsistent managed heap state.
 *
 * Returns 0 on success, -1 on failure (errno is set).
 * On success, *master_fd and *child_pid are populated.
 */
int ryn_pty_spawn(
    const char* command,
    const char* const argv[],
    int* master_fd,
    int* child_pid)
{
    int master;
    pid_t pid = forkpty(&master, NULL, NULL, NULL);

    if (pid < 0)
        return -1;

    if (pid == 0)
    {
        /* Child: exec immediately, no managed code runs here */
        execvp(command, (char* const*)argv);
        _exit(127); /* exec failed */
    }

    /* Parent */
    *master_fd = master;
    *child_pid = (int)pid;
    return 0;
}

#endif /* !_WIN32 */
