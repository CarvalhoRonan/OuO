/*
 * OuO offline SAM helper, 2026. GPL-2.0-only.
 * Separate process based on NTPWEdit 0.7 by Vadim Druzhin and chntpw by
 * Petter Nordahl-Hagen. Complete corresponding source and licenses accompany it.
 * Only edits a workspace copy named SAM. No credential hashes are output.
 */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>
#include "ntpw.h"
static void base64(const unsigned char *bytes,size_t length) {
    const char alphabet[]="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    size_t i;for(i=0;i<length;i+=3){unsigned n=(unsigned)bytes[i]<<16;if(i+1<length)n|=(unsigned)bytes[i+1]<<8;if(i+2<length)n|=bytes[i+2];putchar(alphabet[(n>>18)&63]);putchar(alphabet[(n>>12)&63]);putchar(i+1<length?alphabet[(n>>6)&63]:'=');putchar(i+2<length?alphabet[n&63]:'=');}
}
int main(int argc,char **argv) {
    char *names[H_COUNT]={"SAM"};struct search_user search;struct user_info *user;int rid=0,found=0,ok=0;
    if(argc==2 && !strcmp(argv[1],"--version")){puts("OuO Offline Account 1 / NTPWEdit 0.7 / GPL-2.0-only");return 0;}
    if(argc<2||argc>3||GetFileAttributesA(".ouo-offline-work")==INVALID_FILE_ATTRIBUTES){fputs("Workspace marker required.\n",stderr);return 2;}
    if(strcmp(argv[1],"list") && strcmp(argv[1],"clear") && strcmp(argv[1],"unlock"))return 2;
    if(strcmp(argv[1],"list")){char *end=NULL;long value;if(argc!=3)return 2;value=strtol(argv[2],&end,10);if(!end||*end||value<500||value>0x7FFFFFFF)return 2;rid=(int)value;}
    if(!open_hives(names)){fputs("SAM could not be opened.\n",stderr);return 3;}
    user=first_user(&search);
    while(user){if(!strcmp(argv[1],"list")){printf("USER|%d|",user->rid);base64((const unsigned char *)user->unicode_name,wcslen((wchar_t *)user->unicode_name)*2);printf("|%d\n",is_account_locked(user->rid));}
        if(user->rid==rid)found=1;free(user);user=next_user(&search);}
    if(!strcmp(argv[1],"list")){close_hives();return 0;}
    if(!found||is_hives_ro()){close_hives();return 4;}
    /* Do not set encrypted hashes using the legacy algorithm. Clearing the local
       password is explicit; a new one is set through Windows after sign-in. */
    ok=!strcmp(argv[1],"clear")?change_password(rid,""):unlock_account(rid);
    if(ok && is_hives_dirty())ok=write_hives();
    close_hives();if(!ok){fputs("SAM update failed.\n",stderr);return 5;}puts("OK");return 0;
}
